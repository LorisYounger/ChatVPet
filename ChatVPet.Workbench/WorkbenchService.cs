using System.IO;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VPet.Plugin.ChatVPet;

namespace ChatVPet.Workbench;

public sealed class WorkbenchService : IDisposable
{
    public string Root { get; }
    public string DataDirectory { get; }
    public event Action<string>? Changed;
    private readonly Dictionary<string, WorkbenchSession> sessions = [];
    private readonly Dictionary<string, JObject> saved = [];
    private readonly object gate = new();
    public JObject Scenarios { get; }

    public WorkbenchService(string root)
    {
        Root = Path.GetFullPath(root);
        DataDirectory = Path.Combine(Root, "Local", "Workbench");
        Directory.CreateDirectory(DataDirectory);
        Scenarios = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Scenarios.json")));
        foreach (string path in Directory.EnumerateFiles(DataDirectory, "session.json", SearchOption.AllDirectories))
        {
            try { var state = JObject.Parse(File.ReadAllText(path)); saved.Add(state.Value<string>("id")!, state); }
            catch (JsonException ex) { File.WriteAllText(path + ".read-error.txt", ex.Message); }
        }
    }
    public string[] Configurations() => Directory.GetFiles(Path.Combine(Root, "Local"), "*Settings.json").Select(Path.GetFileName).Cast<string>().ToArray();
    public JArray List()
    {
        lock (gate) return JArray.FromObject(saved.Select(pair => sessions.TryGetValue(pair.Key, out var session) ? session.Overview()
            : new { id = pair.Key, title = pair.Value["options"]!.Value<string>("Title"), status = pair.Value.Value<int>("messageFormat") < 2 ? "旧协议 · 只读" : "已保存", busy = false,
                turnNumber = pair.Value.Value<int>("turnNumber"), events = pair.Value["events"]?.Count() ?? 0,
                compression = pair.Value["options"]!.Value<string>("Compression") }).Reverse());
    }
    public JObject Snapshot(string id)
    {
        lock (gate)
        {
            if (!sessions.ContainsKey(id) && saved.TryGetValue(id, out var state) && state.Value<int>("messageFormat") < 2)
            {
                var archive = (JObject)state.DeepClone();
                archive["status"] = "旧协议 · 只读";
                archive["busy"] = false;
                archive["readOnly"] = true;
                archive["configuration"] ??= new JObject();
                return archive;
            }
        }
        return Get(id).Snapshot();
    }

    public WorkbenchSession Get(string id)
    {
        // Host controls are WPF objects. All lazy construction happens on their owning dispatcher.
        if (!Application.Current.Dispatcher.CheckAccess()) return Application.Current.Dispatcher.Invoke(() => Get(id));
        lock (gate)
        {
            if (sessions.TryGetValue(id, out var existing)) return existing;
            if (!saved.TryGetValue(id, out var state)) throw new KeyNotFoundException("没有这个会话。");
            if (state.Value<int>("messageFormat") < 2) throw new InvalidOperationException("旧协议记录仅供查看和导出，请新建会话。");
            var options = state["options"]!.ToObject<SessionOptions>()!;
            string directory = Path.Combine(DataDirectory, id);
            var config = JsonConvert.DeserializeObject<OpenAIChatConfig>(File.ReadAllText(Path.Combine(directory, "ChatVPetProcess.Settings.json")))!;
            var host = new PluginHost(directory, id, config);
            var session = new WorkbenchSession(id, options, directory, host, state);
            session.Changed = () => Changed?.Invoke(id);
            sessions.Add(id, session);
            return session;
        }
    }
    public WorkbenchSession Create(SessionOptions options, JObject? checkpoint = null, OpenAIChatConfig? sourceConfig = null, string? hostSettings = null)
    {
        if (!Application.Current.Dispatcher.CheckAccess()) return Application.Current.Dispatcher.Invoke(() => Create(options, checkpoint, sourceConfig, hostSettings));
        if (Path.GetFileName(options.Config) != options.Config) throw new ArgumentException("配置需为 Local 目录中的文件名。");
        if (options.Compression is not ("light" or "heavy" or "off" or "custom")) throw new ArgumentException("压缩模式无效。");
        var config = sourceConfig ?? (options.Offline ? new OpenAIChatConfig { UseChatVPetPrompt = true }
            : JsonConvert.DeserializeObject<OpenAIChatConfig>(File.ReadAllText(Path.Combine(Root, "Local", options.Config)))!);
        if (options.DedicatedPrompt.HasValue) config.UseChatVPetPrompt = options.DedicatedPrompt.Value;
        string id = Guid.NewGuid().ToString("N");
        string directory = Path.Combine(DataDirectory, id);
        Directory.CreateDirectory(directory);
        if (hostSettings != null) File.WriteAllText(Path.Combine(directory, "host-settings.lps"), hostSettings);
        var host = new PluginHost(directory, id, config);
        var session = new WorkbenchSession(id, options, directory, host, null);
        if (checkpoint != null) { session.RestoreCheckpoint(checkpoint); session.SaveBranch(); }
        session.Changed = () => Changed?.Invoke(id);
        lock (gate) { sessions.Add(id, session); saved.Add(id, new()); }
        Changed?.Invoke(id);
        return session;
    }
    public string[] RunSuite(string name, SessionOptions options)
    {
        if (!Scenarios.TryGetValue(name, out var suite)) throw new ArgumentException("没有这个测试集。");
        var ids = new List<string>();
        foreach (string profile in name == "compression" ? new[] { "light", "heavy" } : new[] { options.Compression })
        {
            var copy = JObject.FromObject(options).ToObject<SessionOptions>()!;
            copy.Compression = profile; copy.Title = (name == "compression" ? "历史压缩对照" : "基础协议与插件") + " · " + profile;
            if (name == "compression") copy.Rag = false;
            var session = Create(copy);
            session.Start(suite.ToObject<List<TestStep>>()!);
            ids.Add(session.Id);
        }
        return ids.ToArray();
    }
    public string[] Compare(string id, string prompt, string[] variants)
    {
        var source = Get(id);
        JObject state;
        lock (source.Gate)
        {
            if (source.Busy) throw new InvalidOperationException("请等待源会话完成，才能固定相同的对照起点。");
            state = source.Snapshot();
        }
        var ids = new List<string>();
        string group = Guid.NewGuid().ToString("N");
        foreach (string variant in variants)
        {
            var options = state["options"]!.ToObject<SessionOptions>()!;
            options.Title = "对照 · " + variant;
            switch (variant)
            {
                case "baseline": break;
                case "no-rag": options.Rag = false; break;
                case "no-rerank": options.Rerank = false; break;
                case "no-summary": options.Compression = "off"; break;
                case "compatibility": options.DedicatedPrompt = false; break;
                default: throw new ArgumentException("未知对照变量: " + variant);
            }
            var checkpoint = (JObject)state["checkpoint"]!.DeepClone();
            if (variant == "no-summary") checkpoint["summaries"] = new JArray();
            var config = JsonConvert.DeserializeObject<OpenAIChatConfig>(File.ReadAllText(Path.Combine(source.DirectoryPath, "ChatVPetProcess.Settings.json")))!;
            var branch = Create(options, checkpoint, config, source.Host.Settings.ToString());
            branch.Add("comparison", "相同存档起点 · " + variant, new { group, source = id, variant, prompt,
                note = "各分支均从冷缓存开始。关闭 RAG 只关闭知识/记忆注入，工具候选仍按配置召回。关闭摘要恢复完整原文，会改变上下文长度。结果差异是线索，不是因果证明。" });
            branch.Start([new() { Text = prompt }]); ids.Add(branch.Id);
        }
        return ids.ToArray();
    }
    public void Dispose() { foreach (var session in sessions.Values) session.Dispose(); }
}
