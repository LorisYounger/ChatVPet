using System.IO;
using System.Text;
using ChatVPet.ChatProcess;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VPet.Plugin.ChatVPet;
using System.Windows;

namespace ChatVPet.Workbench;

public sealed class SessionOptions
{
    public string Title { get; set; } = "新测试会话";
    public string? Config { get; set; }
    public string? ModDirectory { get; set; }
    public string? SaveFile { get; set; }
    public string Compression { get; set; } = "light";
    public bool Rag { get; set; } = true;
    public bool Rerank { get; set; } = true;
    public bool Cache { get; set; } = true;
    public bool? DedicatedPrompt { get; set; }
    public bool Offline { get; set; }
    public int HistoryThreshold { get; set; } = 20;
    public int KeepRecent { get; set; } = 10;
    public int HistoryCharacters { get; set; } = 32000;
    public int ReferenceCharacters { get; set; } = 12000;
    public int SummaryCharacters { get; set; } = 2400;
    public int RequestCharacters { get; set; } = 48000;
    public double MinimumRerankScore { get; set; } = .1;
}

public sealed class TestStep
{
    public string Text { get; set; } = "";
    public string Expected { get; set; } = "";
    public string Observation { get; set; } = "";
    public bool? Rag { get; set; }
    public double? Money { get; set; }
    public double? Price { get; set; }
    public int? Inventory { get; set; }
    public bool Reload { get; set; }
    public string ImageBase64 { get; set; } = "";
}

public sealed class WorkbenchSession : IDisposable
{
    public string Id { get; }
    public SessionOptions Options { get; }
    public PluginHost Host { get; }
    public VPetChatProcess Process => Host.Plugin.VPetChatProcess;
    public string DirectoryPath { get; }
    public Action? Changed;
    public object Gate { get; } = new();
    public bool Busy { get; private set; }
    private string status = "就绪";
    private readonly JArray events = new();
    private JObject checkpoint = new();
    private ProcessControl control = new();
    private int sequence;
    private int turnNumber;
    private readonly AsyncLocal<string?> purpose = new();
    private readonly AsyncLocal<IReadOnlyList<ChatMessage>?> chatMessages = new();
    private OpenAIChatConfig config => Host.Plugin.OpenAIConfig!;

    public WorkbenchSession(string id, SessionOptions options, string directory, PluginHost host, JObject? saved)
    {
        Id = id; Options = options; DirectoryPath = directory; Host = host;
        if (saved != null)
        {
            events = (JArray)saved["events"]!;
            sequence = events.Last?.Value<int>("seq") ?? 0;
            turnNumber = saved.Value<int?>("turnNumber") ?? 0;
            checkpoint = (JObject)saved["checkpoint"]!;
            RestoreCheckpoint(checkpoint);
            status = saved.Value<bool>("busy") ? "上次运行中断，可查看回执后重试续答" : "已恢复";
        }
        ApplyOptions();
        Host.Trace = (title, data) => Add("host", title, data);
        Host.Plugin.TraceApi = TraceApi;
        Process.Retrieval.Trace = trace => Add("retrieval", "候选召回与重排", trace);
        BindModelCallbacks();
        Process.Checkpoint += PublishCheckpoint;
        Host.Plugin.Save();
        PublishCheckpoint();
    }

    private void BindModelCallbacks()
    {
        var summary = Process.SummaryAPIAskFunction;
        if (summary != null) Process.SummaryAPIAskFunction = (system, messages, tools) =>
        {
            purpose.Value = "summary";
            try { return summary(system, messages, tools); }
            finally { purpose.Value = null; }
        };
        var chat = Process.AIAPIAskFunction!;
        Process.AIAPIAskFunction = (system, messages, tools) =>
        {
            chatMessages.Value = messages;
            try { return chat(system, messages, tools); }
            finally { chatMessages.Value = null; }
        };
        if (Options.Offline)
        {
            Process.AIAPIEmbeddingFunction = null; Process.AIAPIRerankFunction = null;
            Process.ToolAPIAskFunction = null; Process.SummaryAPIAskFunction = null;
            Process.AIAPIAskFunction = (system, messages, tools) =>
            {
                const string reply = "[expression:cheerful]\n已收到这条测试消息。这是离线演示回复。";
                Add("offline", "离线演示 · 模拟收发", new { system, messages, reply });
                return new() { Reply = reply };
            };
        }
    }

    private void ApplyOptions()
    {
        var p = Process;
        p.MaxHistoryBeforeCompress = Options.Compression switch { "off" => 0, "heavy" => 6, "custom" => Options.HistoryThreshold, _ => 20 };
        p.DiaryKeepRecentCount = Options.Compression switch { "heavy" => 2, "custom" => Options.KeepRecent, _ => 10 };
        p.HistoryCharacterBudget = Options.Compression switch { "heavy" => 8000, "custom" => Options.HistoryCharacters, _ => 32000 };
        p.MaxHistoryCount = 10000;
        p.RequestCharacterLimit = Options.RequestCharacters;
        p.SummaryCharacterLimit = Options.SummaryCharacters;
        p.ContextCharacterBudget = Options.ReferenceCharacters;
        p.MemoryLimit = Options.Rag ? 5 : 0; p.RecallMemoryLimit = Options.Rag ? 20 : 0;
        p.MaxKnowledgeCount = Options.Rag ? 4 : 0;
        p.Retrieval.UseCache = Options.Cache;
        p.Retrieval.MinimumRerankScore = (float)Options.MinimumRerankScore;
        if (!Options.Rerank) p.AIAPIRerankFunction = null;
        if (Options.DedicatedPrompt.HasValue)
            p.SystemDescription = ChatProtocol.RenderSystem(Host.Save.GameSave.Name, Host.Save.GameSave.HostName, Options.DedicatedPrompt.Value);
    }

    private void TraceApi(string kind, ModelApiTrace trace)
    {
        if (kind == "operations") kind = purpose.Value ?? (trace.Request["tools"] != null ? "tool" : "vision");
        // Copy payloads at publication because the transport completes the same trace later.
        var data = JObject.FromObject(trace);
        if (kind == "chat" && chatMessages.Value is { } messages)
            data["Sources"] = JArray.FromObject(messages.Select(m => m.FromHost ? "宿主" : m.Role == "user" ? "玩家" : "ChatVPet").Prepend("系统"));
        if (kind == "embedding" && trace.Finished && trace.StatusCode == 200 && trace.Error.Length == 0)
        {
            var response = JObject.Parse(trace.Response);
            data["Response"] = JObject.FromObject(new { count = response["data"]?.Count(),
                dimensions = response["data"]?.First?["embedding"]?.Count(), usage = response["usage"] }).ToString();
        }
        if (kind == "chat" && trace.Finished && trace.StatusCode == 200 && trace.Error.Length == 0)
        {
            var response = JObject.Parse(trace.Response);
            string reply = response["choices"]?[0]?["message"]?.Value<string>("content") ?? "";
            data["InitialProtocolValid"] = ChatProtocol.TryParse(reply, false, out _);
            data["ContinuationProtocolValid"] = ChatProtocol.TryParse(reply, true, out _);
            data["FinishReason"] = response["choices"]?[0]?["finish_reason"]?.DeepClone();
        }
        // Keep the selected image on disk, not megabytes of base64 in every diagnostic snapshot.
        foreach (var image in data.SelectTokens("Request.messages[*].content[*].image_url.url").ToArray())
            image.Replace("(所选图片内容；见本轮图片文件)");
        Add(kind, $"{kind} · {trace.Model} · {(trace.Finished ? trace.Error.Length > 0 ? "失败" : "完成" : "请求中")}", data);
        if (!trace.Finished && kind == "chat")
        {
            long bytes = Encoding.UTF8.GetByteCount(trace.Request["messages"]!.ToString(Formatting.None));
            if (bytes + trace.Request.Value<int>("max_tokens") + 512 > 40960)
                throw new InvalidOperationException("请求超过保守的 40960 token 估算预算，未发送。请压缩历史或减小输入。估算按 UTF-8 字节计，不是精确 tokenizer。 ");
        }
    }

    public void Add(string kind, string title, object data, int? originalTurn = null)
    {
        lock (Gate)
        {
            events.Add(JObject.FromObject(new { seq = ++sequence, time = DateTimeOffset.Now, turn = originalTurn ?? turnNumber, kind, title, data }));
            SaveLocked();
        }
        Changed?.Invoke();
    }

    public JObject Snapshot()
    {
        lock (Gate) return JObject.FromObject(new { id = Id, options = Options, busy = Busy, status, turnNumber,
            events, checkpoint, configuration = PublicConfig(), plugin = typeof(CVPPlugin).Assembly.GetName().ToString() });
    }
    public object Overview()
    {
        lock (Gate) return new { id = Id, title = Options.Title, status, busy = Busy, turnNumber, events = sequence, compression = Options.Compression };
    }
    private JObject PublicConfig()
    {
        var result = JObject.FromObject(config);
        foreach (var property in result.Properties().Where(x => x.Name.Contains("Key", StringComparison.OrdinalIgnoreCase)).ToList()) property.Value = "(不导出)";
        return result;
    }
    private void SaveLocked()
    {
        string path = Path.Combine(DirectoryPath, "session.json");
        File.WriteAllText(path + ".tmp", JsonConvert.SerializeObject(new { messageFormat = 2, id = Id, options = Options, busy = Busy, status, turnNumber, events, checkpoint }));
        // Windows file watchers can briefly hold a just-written snapshot against replacement.
        // Keep the old snapshot intact and retry the same rename; never retry a game action.
        for (int attempt = 0; ; attempt++)
        {
            try { File.Move(path + ".tmp", path, true); break; }
            catch (Exception ex) when (attempt < 4 && ex is IOException or UnauthorizedAccessException)
            { Thread.Sleep(25 * (attempt + 1)); }
        }
    }
    private void PublishCheckpoint()
    {
        var p = Process;
        lock (Gate)
        {
            checkpoint = JObject.FromObject(new { messages = Dialogue.Timeline(p.Dialogues).Select(x => new { turnId = x.TurnId,
                role = x.Message.Role, content = x.Message.Content, source = x.Message.FromHost ? "宿主" : x.Message.Role == "user" ? "玩家" : "ChatVPet" }),
                dialogues = p.Dialogues, diaries = p.DiaryEntries, summaries = p.Summaries,
                host = Host.Snapshot(), p.LastSummaryError, p.LastRetrievalError,
                settings = new { p.MaxHistoryBeforeCompress, p.DiaryKeepRecentCount, p.HistoryCharacterBudget, p.RequestCharacterLimit,
                    p.ContextCharacterBudget, p.MemoryLimit, p.RecallMemoryLimit },
                retrieval = new { p.Retrieval.CacheHits, p.Retrieval.CacheMisses, p.Retrieval.EmbeddingRequests, p.Retrieval.RerankRequests } });
            SaveLocked();
        }
        Changed?.Invoke();
    }
    public void RestoreCheckpoint(JObject state)
    {
        Process.Dialogues = state["dialogues"]!.ToObject<List<Dialogue>>()!;
        Process.DiaryEntries = state["diaries"]!.ToObject<List<DiaryEntry>>()!;
        Process.Summaries = state["summaries"]!.ToObject<List<ConversationSummary>>()!;
        // A branch owns a new isolated save; rewrite ownership, not conversation facts.
        foreach (var dialogue in Process.Dialogues)
        {
            dialogue.SaveId = Process.SaveId;
            if (dialogue.Turn == null) continue;
            dialogue.Turn.SaveId = Process.SaveId;
            foreach (var receipt in dialogue.Turn.Receipts) receipt.SaveId = Process.SaveId;
        }
        foreach (var diary in Process.DiaryEntries) diary.SaveId = Process.SaveId;
        foreach (var summary in Process.Summaries) summary.SaveId = Process.SaveId;
        if (Process.DiaryEntries.Count > 0) DiaryEntry.EnsureCounterAbove(Process.DiaryEntries.Max(x => x.Id));
        Host.Restore((JObject)state["host"]!);
        Process.RecoverInterruptedTurns();
    }

    public void Start(List<TestStep> steps, bool retry = false)
    {
        lock (Gate)
        {
            if (Busy) throw new InvalidOperationException("此会话正在运行，请等待完成或停止后继续。");
            Busy = true; status = retry ? "正在重试续答" : "运行中"; control = new();
            try
            {
                Add("batch", retry ? "续答重试" : $"运行任务 · {steps.Count} 条消息", new { retry,
                    steps = steps.Select(s => new { s.Text, s.Expected, s.Observation, s.Rag, s.Money, s.Price, s.Inventory, s.Reload, imageAttached = s.ImageBase64.Length > 0 }) });
            }
            catch { Busy = false; status = "保存失败，任务未启动"; throw; }
        }
        Changed?.Invoke();
        _ = Task.Run(() =>
        {
            try
            {
                if (retry)
                {
                    var pending = Process.Dialogues.Select(x => x.Turn).LastOrDefault(t => t?.PendingReply == true) ?? throw new InvalidOperationException("没有可以重试的回合。");
                    Process.RetryReply(pending.TurnId, Present);
                }
                else foreach (var step in steps)
                {
                    if (control.ForceToStop) break;
                    turnNumber++;
                    if (step.Reload)
                    {
                        PublishCheckpoint();
                        var saved = JObject.Parse(File.ReadAllText(Path.Combine(DirectoryPath, "session.json")));
                        RestoreCheckpoint((JObject)saved["checkpoint"]!);
                        Add("checkpoint", "从已保存记录重新加载", new { path = "session.json", dialogues = Process.Dialogues.Count });
                    }
                    if (step.Rag.HasValue)
                    {
                        Process.MemoryLimit = step.Rag.Value ? 5 : 0; Process.RecallMemoryLimit = step.Rag.Value ? 20 : 0;
                        Process.MaxKnowledgeCount = step.Rag.Value ? 4 : 0;
                    }
                    if (step.Money.HasValue) Host.Save.GameSave.Money = step.Money.Value;
                    if (step.Price.HasValue) Host.TestBread().Price = step.Price.Value;
                    if (step.Inventory.HasValue)
                    {
                        Host.Items.Clear();
                        if (step.Inventory.Value > 0) { var item = Host.TestBread().Clone(); item.Count = step.Inventory.Value; Host.Items.Add(item); }
                    }
                    Add("user", step.Text, new { text = step.Text, expected = step.Expected, before = Host.Snapshot() });
                    string observation = step.Observation;
                    if (step.ImageBase64.Length > 0)
                    {
                        byte[] bytes = Convert.FromBase64String(step.ImageBase64);
                        File.WriteAllBytes(Path.Combine(DirectoryPath, $"image-{turnNumber}.bin"), bytes);
                        purpose.Value = "vision";
                        try { observation = Options.Offline ? "离线演示未调用视觉模型。" : Host.Plugin.ObserveAttachedImage(step.Text, bytes); }
                        finally { purpose.Value = null; }
                        Add("observation", "图片观察结果", new { observation });
                    }
                    Process.HostState = Host.Plugin.SampleState();
                    Process.Ask(step.Text, observation, Present, control);
                    PublishCheckpoint();
                    var turn = Process.Dialogues.LastOrDefault()?.Turn;
                    Add("turn", "回合完成 · Process 记录", new { turn, summary = Process.ReadSummary(),
                        Process.LastSummaryError, Process.LastRetrievalError, after = Host.Snapshot() });
                    if (step.Expected.Length > 0)
                        Add("review", "预期与实际 · 待人工复核", new { expected = step.Expected,
                            actual = Process.Dialogues.LastOrDefault()?.Answer, receipts = turn?.Receipts, after = Host.Snapshot() });
                }
                lock (Gate) status = control.ForceToStop ? "已停止" : "已完成";
            }
            catch (Exception ex)
            {
                lock (Gate) status = "失败";
                Add("error", "运行失败 · 保留已有记录", new { message = ex.Message, type = ex.GetType().Name, stack = ex.StackTrace });
            }
            finally
            {
                lock (Gate) Busy = false;
                try { PublishCheckpoint(); }
                finally { Changed?.Invoke(); }
            }
        });
    }
    private void Present(ProcessResponse response)
    {
        if (response.Reply.Length == 0 && response.DisplayedList == null) return;
        int? originalTurn;
        lock (Gate) originalTurn = events.LastOrDefault(e => e.Value<string>("kind") == "assistant"
            && e["data"]?.Value<string>("TurnId") == response.TurnId)?.Value<int>("turn");
        Add(response.DisplayedList != null ? "display" : "assistant", response.DisplayedList ?? response.Reply, response, originalTurn);
    }
    public void Stop()
    {
        control.ForceToStop = true;
        Add("control", "已请求停止 · 当前 HTTP 请求结束后停止后续步骤", new { });
    }
    public void Note(string text) => Add("note", "人工分析", new { text });
    public void SaveBranch() => PublishCheckpoint();
    public void ShowPluginWindow(bool settings, Window owner)
    {
        lock (Gate)
        {
            if (Busy) throw new InvalidOperationException("请等待测试完成后打开插件窗口。");
            Busy = true; status = "插件窗口已打开";
        }
        var oldConfig = config;
        try
        {
            // The real settings UI reads the plugin's host settings, so publish the active experiment values there.
            Host.Plugin.MaxHistoryBeforeCompress = Process.MaxHistoryBeforeCompress;
            Host.Plugin.DiaryKeepRecentCount = Process.DiaryKeepRecentCount;
            Host.Plugin.HistoryCharacterBudget = Process.HistoryCharacterBudget;
            Host.Plugin.SummaryCharacterLimit = Process.SummaryCharacterLimit;
            Host.Plugin.RequestCharacterLimit = Process.RequestCharacterLimit;
            Host.Plugin.MaxHistoryCount = Process.MaxHistoryCount;
            Host.Plugin.MaxKnowledgeCount = Process.MaxKnowledgeCount;
            Window window = settings ? new winSetting(Host.Plugin) : new winDiary(Host.Plugin);
            window.Owner = owner;
            window.ShowDialog();
            if (!ReferenceEquals(config, oldConfig))
            {
                Options.Compression = Process.MaxHistoryBeforeCompress <= 0 ? "off" : "custom";
                Options.HistoryThreshold = Process.MaxHistoryBeforeCompress;
                Options.KeepRecent = Process.DiaryKeepRecentCount;
                Options.HistoryCharacters = Process.HistoryCharacterBudget;
                Options.SummaryCharacters = Process.SummaryCharacterLimit;
                Options.RequestCharacters = Process.RequestCharacterLimit;
                Options.DedicatedPrompt = config.UseChatVPetPrompt;
                Options.Rerank = config.RerankEnabled;
                BindModelCallbacks();
                Host.Plugin.Save();
                Add("configuration", "插件设置已应用", PublicConfig());
            }
        }
        finally
        {
            lock (Gate) { Busy = false; status = "就绪"; }
            PublishCheckpoint();
        }
    }

    public object CheckPlugin()
    {
        lock (Gate)
        {
            if (Busy) throw new InvalidOperationException("请等待当前运行结束。");
            Busy = true;
        }
        JObject before = Host.Snapshot();
        var checks = new List<object>();
        void Check(string name, bool passed) { checks.Add(new { name, passed }); }
        try
        {
            Host.Save.GameSave.Money = 10; Host.TestBread().Price = 8; Host.Items.Clear();
            ChatProtocol.TryParse("[delegate][expression:cheerful]\n我先查一下，再按预算买。", false, out var header);
            var turn = new ChatTurn { SaveId = Process.SaveId, Original = "帮我买一个面包，最多花8块，不要吃。", Initial = header };
            HostReceipt Execute(string code, Dictionary<string, string> arguments)
            {
                var pending = new HostReceipt { SaveId = Process.SaveId, TurnId = turn.TurnId, StepId = Guid.NewGuid().ToString("N"), Tool = code, Arguments = arguments };
                turn.Receipts.Add(pending);
                var result = Process.Tools.Single(x => x.Code == code).Execute!(turn, arguments);
                pending.Status = result.Status; pending.Facts = result.Facts; pending.MoneySpent = result.MoneySpent;
                Add("plugin-check", "真实插件工具 · " + code, pending);
                return pending;
            }
            var price = Execute("list_items", new() { ["search"] = "面包" });
            Check("商品目录来自实际插件", price.Status == "observed" && price.Facts.Contains("8.00"));
            var money = Execute("read_state", new() { ["field"] = "money" });
            Check("现场余额", money.Status == "observed" && money.Facts.Contains("10.00"));
            var plan = Execute("plan_action", new() { ["id"] = "test-buy", ["tool"] = "buy_item", ["target"] = "面包", ["quantity"] = "1",
                ["evidence"] = "帮我买一个面包", ["max_total"] = "8", ["unresolved_conditions"] = "" });
            Check("登记成功但未购买", plan.Status == "observed" && Host.Items.Count == 0);
            int eats = Host.EatCalls;
            var purchase = Execute("buy_item", new() { ["request_id"] = "test-buy" });
            Check("真实插件购买入包", purchase.Status == "applied" && Host.Save.GameSave.Money == 2 && Host.Items.Sum(x => x.Count) == 1);
            Check("购买没有食用", Host.EatCalls == eats);
            var duplicate = Execute("buy_item", new() { ["request_id"] = "test-buy" });
            Check("已完成要求不能重复执行", duplicate.Status == "skipped" && Host.Save.GameSave.Money == 2 && Host.Items.Sum(x => x.Count) == 1);
            turn = new ChatTurn { SaveId = Process.SaveId, Original = "帮我买一个面包，最多花7块。", Initial = header };
            Host.Save.GameSave.Money = 10;
            Execute("plan_action", new() { ["id"] = "over-budget", ["tool"] = "buy_item", ["target"] = "面包", ["quantity"] = "1",
                ["evidence"] = "帮我买一个面包", ["max_total"] = "7", ["unresolved_conditions"] = "" });
            var denied = Execute("buy_item", new() { ["request_id"] = "over-budget" });
            Check("实时预算拦截", denied.Status == "skipped" && Host.Save.GameSave.Money == 10);
            Add("plugin-check", "插件自检结果 (无需模型)", checks);
            return checks;
        }
        finally
        {
            try { Host.Restore(before); Host.Plugin.MW.Save(); }
            finally { lock (Gate) Busy = false; PublishCheckpoint(); }
        }
    }
    public void Dispose() => Host.Dispose();
}
