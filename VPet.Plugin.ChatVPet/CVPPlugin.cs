using ChatVPet.ChatProcess;
using LinePutScript;
using LinePutScript.Localization.WPF;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
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
    public SpeechRecognizer? Recognizer;
    public CVPTTalkAPI? TalkAPI;
    public string SideMessage = "";
    public int temptoken;
    public Task Ready { get; private set; } = Task.CompletedTask;
    private GameSave_v2? loadedSave;
    private readonly object saveGate = new();
    private readonly string dataDirectory = Path.Combine(ExtensionValue.BaseDirectory, "ChatVPet");
    public CVPPlugin(IMainWindow mainwin) : base(mainwin) { }
    public override string PluginName => "ChatVPetProcess";

    public override void LoadPlugin()
    {
        Ready = Task.Run(() =>
        {
            string path = Path.Combine(ExtensionValue.BaseDirectory, "ChatVPetProcess.Settings.json");
            OpenAIConfig = File.Exists(path) ? JsonConvert.DeserializeObject<OpenAIChatConfig>(File.ReadAllText(path))! : new OpenAIChatConfig();
            EnsureSave();
            ConfigureVoice();
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
            string path = Path.Combine(dataDirectory, id + ".json");
            if (File.Exists(path))
            {
                var data = JObject.Parse(File.ReadAllText(path));
                process.Dialogues = data["dialogues"]!.ToObject<List<Dialogue>>()!;
                process.DiaryEntries = data["diaries"]!.ToObject<List<DiaryEntry>>()!;
                if (process.DiaryEntries.Count > 0) DiaryEntry.EnsureCounterAbove(process.DiaryEntries.Max(x => x.Id));
            }
            VPetChatProcess = process;
            process.Checkpoint = () => SaveSession(process);
            process.RecoverInterruptedTurns();
            string committed = line[(gstr)"last_receipt"] ?? "";
            if (committed.Length > 0) process.ReceiveReceipt(JsonConvert.DeserializeObject<HostReceipt>(committed)!);
            process.AllowTool = IsToolAllowed;
            process.ScreenshotFunction = ObserveScreenshot;
            process.QuitFunction = FinishQuit;
            process.W2VEngine = new W2VEngine(process);
            ReloadKnowledge();
            RegisterTools();
            ConfigureModels();
            loadedSave = save;
        }
    }

    internal string GetSaveId() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{Environment.UserName}\0{MW.SteamID}\0{MW.PrefixSave}\0{MW.GameSavesData.Data["chatvpet_process"][(gstr)"id"]}")));

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
        string path = Path.Combine(dataDirectory, process.SaveId + ".json");
        string json = JsonConvert.SerializeObject(new { dialogues = process.Dialogues, diaries = process.DiaryEntries });
        using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes);
            stream.Flush(true);
        }
        File.Move(path + ".tmp", path, true);
    }

    public override void Save()
    {
        if (OpenAIConfig != null)
            File.WriteAllText(Path.Combine(ExtensionValue.BaseDirectory, "ChatVPetProcess.Settings.json"), JsonConvert.SerializeObject(OpenAIConfig, Formatting.Indented), Encoding.UTF8);

    }

    public void ConfigureVoice()
    {
        Recognizer?.Dispose(); Recognizer = null;
        if (!AzureVoiceEnable) return;
        var config = SpeechConfig.FromSubscription(AzureKey, AzureRegion);
        config.SpeechRecognitionLanguage = AzureRecognitionLanguage;
        Recognizer = new SpeechRecognizer(config, AudioConfig.FromDefaultMicrophoneInput());
    }

    public override async void Setting()
    {
        try { await Ready; await Task.Run(EnsureSave); new winSetting(this).ShowDialog(); }
        catch (Exception ex) { MW.Main.LabelDisplayShow("ChatVPet: " + ex.Message); }
    }
    public override void EndGame()
    {
        Recognizer?.Dispose();
        chatApi?.Dispose(); toolApi?.Dispose(); embeddingApi?.Dispose(); rerankApi?.Dispose();
    }
}
