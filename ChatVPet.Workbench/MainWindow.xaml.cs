using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using VPet.Plugin.ChatVPet;

namespace ChatVPet.Workbench;

public partial class MainWindow : Window
{
    private readonly WorkbenchService service;
    private string? selected;
    private string image = "";
    private int shownSequence;
    private int refreshPending;
    private bool refreshing;
    private bool raw;
    private readonly ObservableCollection<ChatRow> chat = [];
    private JObject? snapshot;
    public MainWindow(WorkbenchService service, int port)
    {
        InitializeComponent(); this.service = service;
        ApiAddress.Text = $"API  127.0.0.1:{port}";
        ChatItems.ItemsSource = chat;
        service.Changed += OnChanged;
        Refresh();
        Closing += OnClosing;
    }
    private void OnChanged(string id)
    {
        if (Interlocked.Exchange(ref refreshPending, 1) != 0) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { Interlocked.Exchange(ref refreshPending, 0); Refresh(); }));
    }
    private void Refresh()
    {
        refreshing = true;
        var rows = service.List().Select(x => new SessionRow { Id = x.Value<string>("id")!, Label = x.Value<string>("title") + "\n" + x.Value<string>("status") + " · " + x.Value<int>("turnNumber") + " 轮" }).ToArray();
        SessionsList.ItemsSource = rows;
        SessionsList.SelectedItem = rows.FirstOrDefault(x => x.Id == selected);
        refreshing = false;
        if (selected == null) return;
        snapshot = service.Snapshot(selected);
        bool busy = snapshot.Value<bool>("busy");
        SessionTitle.Text = snapshot["options"]!.Value<string>("Title");
        SessionStatus.Text = $"{snapshot.Value<string>("status")}  ·  {snapshot["configuration"]!.Value<string>("Model")}  ·  {snapshot["options"]!.Value<string>("Compression")}  ·  {snapshot.Value<int>("turnNumber")} 轮";
        SendButton.IsEnabled = !busy && !snapshot.Value<bool>("readOnly"); StopButton.IsEnabled = busy;
        Welcome.Visibility = Visibility.Collapsed;
        bool follow = ChatScroll.ScrollableHeight - ChatScroll.VerticalOffset < 80;
        foreach (var entry in snapshot["events"]!.Where(x => x.Value<int>("seq") > shownSequence))
        {
            string kind = entry.Value<string>("kind")!;
            if (kind is "user" or "assistant" or "display" or "error" or "note")
                chat.Add(new() { Kind = kind, Label = $"第 {entry.Value<int>("turn")} 轮 · {kind switch { "user" => "你", "assistant" => "ChatVPet", "display" => "宿主展示", "note" => "人工分析", _ => "运行错误" }}",
                    Text = kind == "note" ? entry["data"]!.Value<string>("text")! : kind == "error" ? entry["data"]!.Value<string>("message")! : entry.Value<string>("title")! });
        }
        shownSequence = snapshot["events"]!.Last?.Value<int>("seq") ?? 0;
        if (follow) ChatScroll.ScrollToEnd();
        RenderEvents();
        var state = (JObject)snapshot["checkpoint"]!;
        ContextDetail.Text = new JObject { ["summary"] = state["summaries"]?.DeepClone(), ["latestTurn"] = state["dialogues"]?.Last?["Turn"]?.DeepClone(),
            ["host"] = state["host"]?.DeepClone(), ["settings"] = state["settings"]?.DeepClone(), ["retrieval"] = state["retrieval"]?.DeepClone(),
            ["LastSummaryError"] = state["LastSummaryError"]?.DeepClone(), ["LastRetrievalError"] = state["LastRetrievalError"]?.DeepClone() }.ToString();
        ConfigDetail.Text = snapshot["configuration"]!.ToString();
    }
    private void RenderEvents()
    {
        if (snapshot == null) return;
        int? seq = (EventsList.SelectedItem as EventRow)?.Seq;
        var rows = snapshot["events"]!.Where(x => Filter.SelectedIndex switch
        {
            1 => x.Value<string>("kind") is "chat" or "tool" or "vision",
            2 => x.Value<string>("kind") is "retrieval" or "embedding" or "rerank",
            3 => x.Value<string>("kind") == "summary",
            4 => x.Value<string>("kind") is "host" or "turn" or "plugin-check",
            5 => x.Value<string>("kind") is "error" or "review" or "note", _ => true
        }).Select(x => new EventRow { Seq = x.Value<int>("seq"), Label = $"{x.Value<int>("seq"):D3} · 第{x.Value<int>("turn")}轮  {x.Value<string>("title")}", Data = (JObject)x }).ToArray();
        EventsList.ItemsSource = rows;
        EventsList.SelectedItem = rows.FirstOrDefault(x => x.Seq == seq) ?? rows.LastOrDefault();
    }
    private void Session_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || SessionsList.SelectedItem is not SessionRow row) return;
        Run(() => Select(row.Id));
    }
    private void Select(string id) { selected = id; shownSequence = 0; chat.Clear(); snapshot = null; Detail.Clear(); Refresh(); }
    private void Event_Selected(object sender, SelectionChangedEventArgs e)
    {
        RenderDetail();
    }
    private void Raw_Click(object sender, RoutedEventArgs e) { raw = !raw; RenderDetail(); }
    private void RenderDetail()
    {
        if (EventsList.SelectedItem is not EventRow row) return;
        var data = row.Data["data"];
        if (!raw && data is JObject obj && obj["Request"] is JObject request)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine(row.Label).AppendLine(obj.Value<string>("Endpoint"));
            text.AppendLine($"模型: {obj.Value<string>("Model")}  HTTP: {obj.Value<int>("StatusCode")}  耗时: {obj.Value<double>("Milliseconds"):F0} ms");
            text.AppendLine($"temperature: {request["temperature"]}  max_tokens: {request["max_tokens"]}");
            int index = 0;
            foreach (var message in request["messages"] ?? new JArray())
            {
                string? source = obj["Sources"]?[index++]?.Value<string>();
                text.AppendLine().AppendLine($"━━ {message["role"]}{(source == null ? "" : " · " + source)} ━━").AppendLine(message["content"]?.ToString());
            }
            if (request["messages"] == null) text.AppendLine(request.ToString());
            if (request["tools"] != null) text.AppendLine().AppendLine("━━ 可用工具 ━━").AppendLine(request["tools"]!.ToString());
            text.AppendLine().AppendLine("━━ 模型原始返回 ━━").AppendLine(obj.Value<string>("Response"));
            string response = obj.Value<string>("Response") ?? "";
            try
            {
                var result = JObject.Parse(response);
                string? content = result["choices"]?[0]?["message"]?.Value<string>("content");
                if (content != null) text.AppendLine().AppendLine("━━ 回复正文 ━━").AppendLine(content);
            }
            catch (Newtonsoft.Json.JsonException) { }
            if (obj.Value<string>("Error")?.Length > 0) text.AppendLine("错误: " + obj.Value<string>("Error"));
            Detail.TextWrapping = TextWrapping.Wrap; Detail.Text = text.ToString();
        }
        else if (!raw && row.Data.Value<string>("kind") == "retrieval")
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine($"{data!["kind"]} · {data["candidateCount"]} 个候选 · 返回上限 {data["limit"]}");
            text.AppendLine($"缓存命中 {data["cacheHits"]} · 服务异常 {data["ServiceUnavailable"]}");
            text.AppendLine(data.Value<string>("error"));
            text.AppendLine("召回分数越低越相关；重排分数越高越相关。\n入选后还会经过去重与上下文预算，最终内容见本轮上下文。");
            foreach (var item in data["ranked"] ?? new JArray())
                text.AppendLine($"{(item.Value<bool>("selected") ? "✓ 入选" : "· 未选")}  召回 {item["recallScore"]}  重排 {item["rerankScore"]}").AppendLine(item.Value<string>("text")).AppendLine();
            Detail.TextWrapping = TextWrapping.Wrap; Detail.Text = text.ToString();
        }
        else { Detail.TextWrapping = TextWrapping.NoWrap; Detail.Text = row.Data.ToString(); }
    }
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) RenderEvents(); }
    private void Run(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "ChatVPet Lab"); } }
    private WorkbenchSession Current => selected != null ? service.Get(selected) : throw new InvalidOperationException("请先新建或选择会话。");
    private SessionOptions? AskOptions()
    {
        var dialog = new NewSessionWindow(service.Configurations()) { Owner = this, Resources = Resources };
        return dialog.ShowDialog() == true ? dialog.Options : null;
    }
    private void New_Click(object sender, RoutedEventArgs e) => Run(() => { var options = AskOptions(); if (options != null) Select(service.Create(options).Id); });
    private void Baseline_Click(object sender, RoutedEventArgs e) => Run(() => { var options = AskOptions(); if (options != null) Select(service.RunSuite("baseline", options)[0]); });
    private void Compression_Click(object sender, RoutedEventArgs e) => Run(() => { var options = AskOptions(); if (options != null) Select(service.RunSuite("compression", options)[0]); });
    private void Send_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (string.IsNullOrWhiteSpace(Input.Text)) return;
        Current.Start([new() { Text = Input.Text, ImageBase64 = image.Length == 0 ? "" : Convert.ToBase64String(File.ReadAllBytes(image)) }]);
        Input.Clear(); image = ""; Attachment.Visibility = Visibility.Collapsed;
    });
    private void Input_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Send_Click(sender, e); e.Handled = true; } }
    private void Stop_Click(object sender, RoutedEventArgs e) => Run(() => Current.Stop());
    private void Retry_Click(object sender, RoutedEventArgs e) => Run(() => Current.Start([], true));
    private void Image_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg" };
        if (dialog.ShowDialog(this) == true) { image = dialog.FileName; Attachment.Text = "附图: " + Path.GetFileName(image); Attachment.Visibility = Visibility.Visible; }
    }
    private void Compare_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (string.IsNullOrWhiteSpace(Input.Text)) throw new InvalidOperationException("请在消息输入框填写要对照的问题。");
        Select(service.Compare(Current.Id, Input.Text, ["baseline", (string)((ComboBoxItem)Variant.SelectedItem).Tag])[0]);
    });
    private void Note_Click(object sender, RoutedEventArgs e) => Run(() => { if (Note.Text.Length > 0) { Current.Note(Note.Text); Note.Clear(); } });
    private void Export_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var dialog = new SaveFileDialog { Filter = "JSON 诊断记录|*.json", FileName = "chatvpet-" + selected + ".json" };
        if (dialog.ShowDialog(this) == true) File.WriteAllText(dialog.FileName, service.Snapshot(selected!).ToString());
    });
    private void Copy_Click(object sender, RoutedEventArgs e) => Run(() => Clipboard.SetText(Detail.Text));
    private void Folder_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(service.DataDirectory) { UseShellExecute = true });
    private void PluginSettings_Click(object sender, RoutedEventArgs e) => Run(() => Current.ShowPluginWindow(true, this));
    private void PluginDiary_Click(object sender, RoutedEventArgs e) => Run(() => Current.ShowPluginWindow(false, this));
    private void PluginCheck_Click(object sender, RoutedEventArgs e) => Run(() => Current.CheckPlugin());
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (service.List().Any(x => x.Value<bool>("busy")))
        {
            e.Cancel = true; MessageBox.Show(this, "仍有测试或插件窗口正在运行。请先停止测试并关闭插件窗口，待当前请求结束后关闭。", "ChatVPet Lab"); return;
        }
        service.Changed -= OnChanged; Application.Current.Shutdown();
    }
    private sealed class SessionRow { public string Id { get; set; } = ""; public string Label { get; set; } = ""; }
    private sealed class EventRow { public int Seq { get; set; } public string Label { get; set; } = ""; public JObject Data { get; set; } = new(); }
    private sealed class ChatRow { public string Kind { get; set; } = ""; public string Label { get; set; } = ""; public string Text { get; set; } = ""; }
}
