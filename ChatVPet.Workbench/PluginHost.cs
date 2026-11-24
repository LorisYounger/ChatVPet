using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ChatVPet.ChatProcess;
using LinePutScript;
using Newtonsoft.Json.Linq;
using VPet.Plugin.ChatVPet;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;

namespace ChatVPet.Workbench;

/// <summary>A small explicit host for the actual plugin. Unsupported host calls fail visibly.</summary>
public sealed class PluginHost : IDisposable
{
    public CVPPlugin Plugin { get; }
    public GameSave_v2 Save { get; } = new("萝莉斯");
    public List<Item> Items { get; } = [];
    public List<Food> Foods { get; } = [new() { Name = "面包", Price = 8, StrengthFood = 10 }];
    public LpsDocument Settings { get; } = new();
    public Main Pet { get; }
    public int SaveCalls { get; private set; }
    public int EatCalls { get; private set; }
    public Action<string, object>? Trace;
    private readonly string directory;

    public PluginHost(string directory, string id, OpenAIChatConfig config)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
        string settingsPath = Path.Combine(directory, "host-settings.lps");
        if (File.Exists(settingsPath)) Settings.Load(File.ReadAllText(settingsPath));
        Save.Data["chatvpet_process"][(gstr)"id"] = id;
        Save.GameSave.Money = 10;
        Save.GameSave.HostName = "测试员";
        var controller = DispatchProxy.Create<IController, SandboxProxy>();
        ((SandboxProxy)(object)controller).Call = (method, _) => method.Name switch
        {
            "get_EnableFunction" => true,
            "get_ZoomRatio" => 1.0,
            _ => throw new NotSupportedException("测试宿主未实现控制器: " + method.Name)
        };
        var core = new GameCore { Save = Save.GameSave, Controller = controller,
            Graph = new GraphCore(500, Application.Current.Dispatcher, new GraphCore.Config(new LpsDocument())) };
        Pet = new Main(core);
        Pet.EventTimer.Stop();
        var setting = DispatchProxy.Create<ISetting, SandboxProxy>();
        ((SandboxProxy)(object)setting).Call = (method, args) => method.Name switch
        {
            "get_Item" => Settings[(string)args![0]!],
            "get_EnableFunction" => true,
            "get_Language" => "zh-Hans",
            "get_ZoomLevel" => 1.0,
            _ => throw new NotSupportedException("测试宿主未实现设置: " + method.Name)
        };
        var host = DispatchProxy.Create<IMainWindow, SandboxProxy>();
        var petGrid = new Grid { Width = 500, Height = 500 };
        ((SandboxProxy)(object)host).Call = (method, args) =>
        {
            switch (method.Name)
            {
                case "get_Core": return core;
                case "get_Main": return Pet;
                case "get_Set": return setting;
                case "get_GameSavesData": return Save;
                case "get_PrefixSave": return "workbench-" + id;
                case "get_SteamID": return 0UL;
                case "get_HashCheck": return false;
                case "get_Foods": return Foods;
                case "get_Items": return Items;
                case "get_Dispatcher": return Application.Current.Dispatcher;
                case "get_PetGrid": return petGrid;
                case "ItemsAdd":
                    var item = (Item)args![0]!;
                    var existing = Items.Find(x => x.Name == item.Name);
                    if (existing == null) Items.Add(item); else existing.Count += item.Count;
                    Trace?.Invoke("背包入库 (模拟宿主)", new { item.Name, item.Count });
                    return null;
                case "Save":
                    SaveCalls++;
                    File.WriteAllText(settingsPath, Settings.ToString());
                    File.WriteAllText(Path.Combine(directory, "host.json"), Snapshot().ToString());
                    Trace?.Invoke("宿主保存 (隔离目录)", new { SaveCalls });
                    return null;
                case "TakeItem":
                    var food = (Food)args![0]!;
                    Save.GameSave.StrengthFood += food.StrengthFood;
                    EatCalls++;
                    Trace?.Invoke("食用效果 (模拟宿主)", new { food.Name, EatCalls });
                    return null;
                case "TakeItemHandle":
                case "DisplayFoodAnimation":
                    Trace?.Invoke("演出请求 (仅记录，未播放)", new { method = method.Name });
                    return null;
                default: throw new NotSupportedException("测试宿主未实现: " + method.Name);
            }
        };
        Plugin = new CVPPlugin(host, directory) { OpenAIConfig = config };
        // Initialize the plugin's actual configuration, knowledge, tools and save lifecycle.
        // LoadPlugin also installs the game's toolbar/TalkBox, which this workbench does not impersonate.
        Plugin.EnsureSave();
    }

    public JObject Snapshot() => JObject.FromObject(new { money = Save.GameSave.Money,
        price = Foods[0].Price, inventory = Items.Select(x => new { x.Name, x.Count }),
        feeling = Save.GameSave.Feeling, affinity = Save.GameSave.Likability, food = Save.GameSave.StrengthFood,
        SaveCalls, EatCalls, receipt = Save.Data["chatvpet_process"][(gstr)"last_receipt"] ?? "" });

    public void Restore(JObject data)
    {
        Save.GameSave.Money = data.Value<double?>("money") ?? 10;
        Foods[0].Price = data.Value<double?>("price") ?? 8;
        Items.Clear();
        foreach (var row in data["inventory"] ?? new JArray())
        {
            var item = Foods[0].Clone(); item.Name = row.Value<string>("Name")!; item.Count = row.Value<int>("Count"); Items.Add(item);
        }
        SaveCalls = data.Value<int?>("SaveCalls") ?? 0;
        EatCalls = data.Value<int?>("EatCalls") ?? 0;
        Save.GameSave.Feeling = data.Value<double?>("feeling") ?? Save.GameSave.Feeling;
        Save.GameSave.Likability = data.Value<double?>("affinity") ?? Save.GameSave.Likability;
        Save.GameSave.StrengthFood = data.Value<double?>("food") ?? Save.GameSave.StrengthFood;
        Save.Data["chatvpet_process"][(gstr)"last_receipt"] = data.Value<string>("receipt") ?? "";
    }
    public void Dispose() { Plugin.EndGame(); Pet.Dispose(); Pet.Core.Graph?.Dispose(); }
}

public class SandboxProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Call = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!, args);
}
