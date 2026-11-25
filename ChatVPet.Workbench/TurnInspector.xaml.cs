using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace ChatVPet.Workbench;

public partial class TurnInspector : UserControl
{
    private sealed record Round(int Number, string Label);
    private sealed record Change(string Name, string Before, string After);
    private sealed record Step(string Name, string State, string Color, string Detail);
    private sealed record Call(string Key, string Label, JToken Entry);
    private sealed record Message(string Label, string Body);
    private JObject? snapshot;
    private int? pinned;
    private int? displayedRound;
    private int lastCallSequence = -1;
    private bool updating;
    public event Action<int>? RoundSelected;

    public TurnInspector() => InitializeComponent();

    public void Update(JObject state)
    {
        bool newSession = (string?)state["id"] != (string?)snapshot?["id"];
        snapshot = state;
        if (newSession) { pinned = null; displayedRound = null; Pages.SelectedIndex = 0; }
        var rounds = state["events"]!.Where(e => e.Value<int>("turn") > 0 || (string?)e["kind"] is "plugin-check" or "error")
            .GroupBy(e => e.Value<int>("turn")).OrderBy(g => g.Key).Select(group =>
            {
                string question = (string?)group.FirstOrDefault(e => (string?)e["kind"] == "user")?["title"] ?? "";
                return new Round(group.Key, group.Key == 0 ? "会话检查" : $"第 {group.Key} 轮  {question.ReplaceLineEndings(" ")}");
            }).ToList();
        updating = true;
        if (newSession || !RoundPicker.Items.Cast<Round>().SequenceEqual(rounds)) RoundPicker.ItemsSource = rounds;
        RoundPicker.SelectedValue = pinned ?? rounds.LastOrDefault()?.Number;
        updating = false;
        RoundPicker.Visibility = Visible(rounds.Count > 0);
        DrawRound();
    }

    public void SelectTurn(int turn)
    {
        RoundPicker.SelectedValue = turn;
        pinned = turn;
        Pages.SelectedIndex = 0;
    }

    private void RoundChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating) return;
        pinned = (RoundPicker.SelectedItem as Round)?.Number;
        DrawRound();
        if (pinned is int turn) RoundSelected?.Invoke(turn);
    }

    private void DrawRound()
    {
        int? number = (RoundPicker.SelectedItem as Round)?.Number;
        var entries = snapshot?["events"]?.Where(e => number != null && e.Value<int>("turn") == number).ToList() ?? [];
        JToken? Last(string kind) => entries.LastOrDefault(e => (string?)e["kind"] == kind)?["data"];
        var user = Last("user");
        var result = Last("turn");
        var checks = entries.LastOrDefault(e => (string?)e["kind"] == "plugin-check" && e["data"] is JArray)?["data"] as JArray;
        string? error = (string?)Last("error")?["message"];
        var steps = new List<Step>();
        if (result?["turn"]?["Receipts"] is JArray receipts)
            foreach (var r in receipts)
            {
                string tool = (string?)r["Tool"] ?? "";
                string status = (string?)r["Status"] ?? "";
                if ((string?)r["StepId"] == "finish" || tool == "plan_action" && status == "observed") continue;
                string name = tool switch
                {
                    "list_items" => "查询商品", "read_state" => "读取桌宠状态", "buy_item" => "购买物品", "eat_item" => "食用物品",
                    "list_activities" => "查询活动", "start_activity" => "开始活动", "stop_activity" => "停止活动", "adjust_state" => "调整状态",
                    "search_animations" => "查询动画", "play_animation" => "播放动画", "plan_action" => "登记行动", _ => tool
                };
                string state = status switch { "applied" => "已执行", "observed" => "已查询", "skipped" => "已跳过", "failed" => "失败", _ => "待核对" };
                steps.Add(new(name, state, status is "applied" or "observed" ? "#95D9C4" : "#E5BC89", (string?)r["Facts"] ?? ""));
            }
        if (checks != null)
            foreach (var row in checks)
            {
                bool ok = (bool?)row["passed"] == true;
                steps.Add(new((string?)row["name"] ?? "", ok ? "通过" : "失败", ok ? "#95D9C4" : "#F6ABA9", ok ? "检查结果符合预期。" : "检查结果与预期不符。"));
            }
        if (!string.IsNullOrEmpty(error)) steps.Add(new("运行错误", "失败", "#F6ABA9", error));
        var changes = CompareState(user?["before"], result?["after"]);
        if (Changes.ItemsSource is not List<Change> previousChanges || !previousChanges.SequenceEqual(changes))
            Changes.ItemsSource = changes;
        if (displayedRound != number || Actions.ItemsSource is not List<Step> previousSteps || !previousSteps.SequenceEqual(steps))
            Actions.ItemsSource = steps;
        ChangeSection.Visibility = Visible(changes.Count > 0);
        ActionSection.Visibility = Visible(steps.Count > 0);
        Expected.Text = (string?)user?["expected"] ?? (string?)Last("review")?["expected"] ?? "";
        ExpectSection.Visibility = Visible(Expected.Text.Length > 0);
        bool failed = error != null || steps.Any(s => s.State == "失败");
        bool offline = entries.Any(e => (string?)e["kind"] == "offline");
        Outcome.Text = failed ? "有错误需要查看" : checks != null ? $"自检通过 {checks.Count(r => (bool?)r["passed"] == true)} / {checks.Count}" :
            result != null ? changes.Count > 0 ? "桌宠存档已更新" : offline ? "已收到离线模拟回复" : "本轮对话完成" :
            number == null ? "等待测试消息" : (bool?)snapshot?["busy"] == true ? "正在测试…" : "本轮未记录完成结果";
        StatusIcon.Foreground = failed ? System.Windows.Media.Brushes.LightSalmon : System.Windows.Media.Brushes.MediumAquamarine;
        StatusIcon.Text = failed ? "!" : result != null || checks != null ? "✓" : "○";
        var calls = entries.Where(e => (string?)e["kind"] is "chat" or "offline" or "tool" or "vision" or "summary").ToList();
        int seq = calls.LastOrDefault()?.Value<int>("seq") ?? 0;
        if (displayedRound != number || lastCallSequence != seq || number == null)
        {
            DrawCalls(calls, displayedRound == number);
            lastCallSequence = seq;
        }
        displayedRound = number;
    }

    private static List<Change> CompareState(JToken? before, JToken? after)
    {
        var changes = new List<Change>();
        if (before == null || after == null) return changes;
        foreach (var (key, label) in new[] { ("money","余额"), ("food","饱腹"), ("feeling","心情"), ("affinity","好感"), ("EatCalls","食用次数") })
            if ((double?)before[key] is double oldValue && (double?)after[key] is double newValue && oldValue != newValue)
                changes.Add(new(label, oldValue.ToString("0.##"), newValue.ToString("0.##")));
        if (before["inventory"] is JArray oldItems && after["inventory"] is JArray newItems)
        {
            static Dictionary<string,int> Counts(JArray items) => items.GroupBy(i => (string?)i["Name"] ?? "").ToDictionary(g => g.Key, g => g.Sum(i => (int?)i["Count"] ?? 0));
            var oldCounts = Counts(oldItems); var newCounts = Counts(newItems);
            foreach (string name in oldCounts.Keys.Union(newCounts.Keys))
            {
                int oldCount = oldCounts.GetValueOrDefault(name), newCount = newCounts.GetValueOrDefault(name);
                if (oldCount != newCount) changes.Add(new(name, oldCount.ToString(), newCount.ToString()));
            }
        }
        return changes;
    }

    private void DrawCalls(List<JToken> entries, bool sameRound)
    {
        var selected = sameRound ? Calls.SelectedItem as Call : null;
        int message = sameRound ? Messages.SelectedIndex : -1;
        var rows = entries.GroupBy(e => (string?)e["data"]?["Id"] ?? e["seq"]!.ToString()).Select((g, index) =>
        {
            var last = g.Last();
            return new Call(g.Key, $"{index + 1}.  {DiagnosticText.Kind((string)last["kind"]!)}", last);
        }).ToList();
        updating = true;
        Calls.ItemsSource = rows;
        Calls.SelectedItem = rows.Find(c => c.Key == selected?.Key) ?? rows.FirstOrDefault(c => (string?)c.Entry["kind"] is "chat" or "offline") ?? rows.FirstOrDefault();
        updating = false;
        Calls.Visibility = WireTabs.Visibility = Visible(rows.Count > 0);
        NoCalls.Visibility = Visible(rows.Count == 0);
        DrawMessages(selected != null && selected.Key == (Calls.SelectedItem as Call)?.Key ? message : -1);
    }

    private void CallChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!updating) DrawMessages(-1);
    }

    private void DrawMessages(int preferred)
    {
        if (Calls.SelectedItem is not Call call) { Messages.ItemsSource = null; InputBody.Clear(); OutputBody.Clear(); return; }
        var data = call.Entry["data"]!;
        var offline = (string?)call.Entry["kind"] == "offline";
        var rows = new List<Message>();
        if (offline) rows.Add(new("系统提示", (string?)data["system"] ?? ""));
        var messages = (offline ? data["messages"] : data["Request"]?["messages"]) as JArray;
        for (int i = 0; messages != null && i < messages.Count; ++i)
        {
            var m = messages[i];
            string? role = (string?)(m["role"] ?? m["Role"]);
            bool host = (bool?)m["FromHost"] == true || (data["Sources"] as JArray)?.ElementAtOrDefault(i)?.Value<string>() == "宿主";
            bool chat = offline || (string?)call.Entry["kind"] == "chat";
            string sender = role switch
            {
                "system" => "系统提示", "assistant" => "历史回复", "user" when host => "宿主回传",
                "user" when chat => "玩家输入", "user" => "任务输入", _ => role ?? "消息"
            };
            var content = m["content"] ?? m["Content"];
            string body = content is JArray parts ? string.Join("\n", parts.Select(p => (string?)p["type"] == "image_url" ? "〔本轮附图〕" : (string?)p["text"])) : (string?)content ?? "";
            rows.Add(new($"{rows.Count + 1}.  {sender}", body));
        }
        Messages.ItemsSource = rows;
        Messages.SelectedIndex = preferred >= 0 && preferred < rows.Count ? preferred : rows.Count - 1;
        OutputBody.Text = DiagnosticText.Response(call.Entry);
    }

    private void MessageChanged(object sender, SelectionChangedEventArgs e) => InputBody.Text = (Messages.SelectedItem as Message)?.Body ?? "";
    private static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
