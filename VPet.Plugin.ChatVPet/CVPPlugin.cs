using ChatVPet.ChatProcess;
using LinePutScript;
using LinePutScript.Localization.WPF;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.ChatVPet;

public partial class CVPPlugin : MainPlugin
{
    public VPetChatProcess VPetChatProcess = new();
    public OpenAIChatConfig? OpenAIConfig;
    public CVPTTalkAPI? TalkAPI;
    public string SideMessage = "";
    public int temptoken;
    public Task Ready { get; private set; } = Task.CompletedTask;
    private GameSave_v2? loadedSave;
    private readonly object saveGate = new();
    private readonly string dataDirectory;
    private readonly string settingsPath;
    public CVPPlugin(IMainWindow mainwin) : this(mainwin, ExtensionValue.BaseDirectory) { }
    public CVPPlugin(IMainWindow mainwin, string storageDirectory) : base(mainwin)
    {
        dataDirectory = Path.Combine(storageDirectory, "ChatVPet");
        settingsPath = Path.Combine(storageDirectory, "ChatVPetProcess.Settings.json");
    }
    public override string PluginName => "ChatVPetProcess";

    public override void LoadPlugin()
    {
        Ready = Task.Run(() =>
        {
            string path = settingsPath;
            OpenAIConfig = File.Exists(path) ? JsonConvert.DeserializeObject<OpenAIChatConfig>(File.ReadAllText(path))! : new OpenAIChatConfig();
            EnsureSave();
        });
        TalkAPI = new CVPTTalkAPI(this);
        MW.TalkAPI.Add(TalkAPI);
        var settings = new MenuItem { Header = "ChatVPetProcess" };
        settings.Click += (_, _) => Setting();
        MW.Main.ToolBar.MenuMODConfig.Items.Add(settings);
        var diary = new MenuItem { Header = "桌宠日记".Translate() };
        diary.Click += async (_, _) =>
        {
            try { await Ready; await Task.Run(EnsureSave); new winDiary(this).ShowDialog(); }
            catch (Exception ex) { MW.Main.LabelDisplayShow("ChatVPet: " + ex.Message); }
        };
        MW.Main.ToolBar.MenuMODConfig.Items.Add(diary);

    }

    public void EnsureSave()
    {
        lock (saveGate)
        {
            if (ReferenceEquals(loadedSave, MW.GameSavesData) && VPetChatProcess.SaveId == GetSaveId()) return;
            // Session changes are persisted by Process.Checkpoint before and after each operation.
            var save = MW.GameSavesData;
            var line = save.Data["chatvpet_process"];
            string id = line[(gstr)"id"] ?? "";
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                line[(gstr)"id"] = id;
                MW.Save();
            }
            id = GetSaveId();
            var process = new VPetChatProcess { SaveId = id };
            string path = Path.Combine(dataDirectory, id + ".turns.json");
            if (File.Exists(path))
            {
                var data = JObject.Parse(File.ReadAllText(path));
                process.Dialogues = data["dialogues"]!.ToObject<List<Dialogue>>()!;
                process.DiaryEntries = data["diaries"]!.ToObject<List<DiaryEntry>>()!;
                process.Summaries = data["summaries"]?.ToObject<List<ConversationSummary>>() ?? [];
                if (process.DiaryEntries.Count > 0) DiaryEntry.EnsureCounterAbove(process.DiaryEntries.Max(x => x.Id));
            }
            VPetChatProcess = process;
            process.Checkpoint = () => SaveSession(process);
            process.RecoverInterruptedTurns();
            string committed = line[(gstr)"last_receipt"] ?? "";
            if (committed.Length > 0) process.ReceiveReceipt(JsonConvert.DeserializeObject<HostReceipt>(committed)!);
            var host = new GameHost(MW, process, GetSaveId, "chatvpet_process");
            process.AllowTool = host.IsToolAllowed;
            process.ScreenshotFunction = ObserveScreenshot;
            process.QuitFunction = FinishQuit;
            ReloadKnowledge();
            host.RegisterTools();
            ConfigureModels();
            loadedSave = save;
        }
    }

    internal string GetSaveId() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{Environment.UserName}\0{MW.SteamID}\0{MW.PrefixSave}\0{MW.GameSavesData.Data["chatvpet_process"][(gstr)"id"]}")));

    internal void ImportCloud(string file, string target)
    {
        var data = JObject.Parse(File.ReadAllText(file));
        var settings = data["settings"] ?? throw new InvalidDataException("缺少模型设置。");
        var chat = (string?)settings["Model"] ?? throw new InvalidDataException("缺少聊天模型。");
        var tool = (string?)settings["ToolModel"] ?? throw new InvalidDataException("缺少 ToolCall 模型。");
        var dialogues = data["dialogues"]!.ToObject<List<Dialogue>>()!;
        var diaries = data["diaries"]!.ToObject<List<DiaryEntry>>()!;
        var summaries = data["summaries"]!.ToObject<List<ConversationSummary>>()!;
        lock (saveGate)
        {
            EnsureSave();
            if (target != GetSaveId()) throw new InvalidOperationException("目标存档已经切换。");
            VPetChatProcess.ImportConversation(dialogues, diaries, summaries);
            OpenAIConfig!.Model = chat;
            OpenAIConfig.ToolModel = tool;
            OpenAIConfig.UseChatVPetPrompt = (bool?)settings["UseChatVPetPrompt"] == true;
            if ((string?)settings["EmbeddingModel"] is string embedding) OpenAIConfig.EmbeddingModel = embedding;
            Save();
            ConfigureModels();
        }
    }

    public void ReloadKnowledge()
    {
        var process = VPetChatProcess;
        process.KnowledgeDataBases.Clear();
        process.AddKnowledgeDataBase(Properties.Resources.VpetKnowledgeDataBase.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries));
        process.AddKnowledgeDataBase(KnowledgeDataBase.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        foreach (var item in MW.Foods) process.AddKnowledgeDataBase($"物品: {item.TranslateName}; {item.Description}");
        foreach (var work in MW.Core.Graph!.GraphConfig.Works) process.AddKnowledgeDataBase($"活动: {work.NameTrans}; {work.Type}; {work.Time}分钟");
        // Numeric game state is exposed only by read_state/list_items, never as cached knowledge.
    }

    public string SampleState()
    {
        var now = DateTime.Now;
        string state = $"{now:yyyy-MM-dd} 周{"日一二三四五六"[(int)now.DayOfWeek]} {now:HH:mm}";
        if (MW.Set.EnableFunction)
        {
            string mode = MW.Core.Save!.Mode.ToString() switch { "Happy" => "开心", "PoorCondition" => "不开心", "Ill" => "生病", _ => "普通" };
            state += $", 状态:{mode}";
            if (double.IsFinite(MW.Core.Save!.Likability)) state += ", 好感:" + ChatProtocol.AffinityTier(MW.Core.Save!.Likability);
        }
        return state;
    }

    private void SaveSession(VPetChatProcess process)
    {
        Directory.CreateDirectory(dataDirectory);
        string path = Path.Combine(dataDirectory, process.SaveId + ".turns.json");
        string json = JsonConvert.SerializeObject(new { dialogues = process.Dialogues, diaries = process.DiaryEntries, summaries = process.Summaries });
        using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes);
            stream.Flush(true);
        }
        // File indexing on Windows can briefly deny replacement immediately after a checkpoint write.
        for (int attempt = 0; ; attempt++)
        {
            try { File.Move(path + ".tmp", path, true); break; }
            catch (Exception ex) when (attempt < 4 && ex is IOException or UnauthorizedAccessException)
            { System.Threading.Thread.Sleep(25 * (attempt + 1)); }
        }
    }

    public override void Save()
    {
        if (OpenAIConfig == null) return;
        File.WriteAllText(settingsPath + ".tmp", JsonConvert.SerializeObject(OpenAIConfig, Formatting.Indented), Encoding.UTF8);
        File.Move(settingsPath + ".tmp", settingsPath, true);
    }

    public override async void Setting()
    {
        try { await Ready; await Task.Run(EnsureSave); new winSetting(this).ShowDialog(); }
        catch (Exception ex) { MW.Main.LabelDisplayShow("ChatVPet: " + ex.Message); }
    }
    public override void EndGame()
    {
        chatApi?.Dispose(); toolApi?.Dispose(); embeddingApi?.Dispose(); rerankApi?.Dispose(); searchApi?.Dispose();
    }
}
