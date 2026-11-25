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
    private readonly ObservableCollection<EventRow> visibleEvents = [];
    private JObject? snapshot;
    public MainWindow(WorkbenchService service, int port)
    {
        InitializeComponent(); this.service = service;
        MoreButton.ToolTip = $"本机 API · 127.0.0.1:{port}";
        MoreButton.IsEnabled = SendButton.IsEnabled = false;
        ChatItems.ItemsSource = chat;
        Inspector.RoundSelected += turn =>
        {
            var row = chat.FirstOrDefault(c => c.Turn == turn && c.Kind == "user");
            if (row != null && ChatItems.ItemContainerGenerator.ContainerFromItem(row) is FrameworkElement bubble)
                ChatScroll.ScrollToVerticalOffset(ChatScroll.VerticalOffset + bubble.TranslatePoint(new Point(), ChatScroll).Y);
        };
        EventsList.ItemsSource = visibleEvents;
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
        SessionStatus.Text = string.Join("  ·  ", new[] { (string?)snapshot["status"], (string?)snapshot["configuration"]?["Model"],
            $"{snapshot.Value<int>("turnNumber")} 轮" }.Where(x => !string.IsNullOrWhiteSpace(x)));
        Composer.Visibility = snapshot.Value<bool>("readOnly") ? Visibility.Collapsed : Visibility.Visible;
        SendButton.IsEnabled = !busy && !snapshot.Value<bool>("readOnly"); StopButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        MoreButton.IsEnabled = true;
        Welcome.Visibility = Visibility.Collapsed;
        bool follow = ChatScroll.ScrollableHeight - ChatScroll.VerticalOffset < 80;
        int oldChatCount = chat.Count;
        int previousSequence = shownSequence;
        foreach (var entry in snapshot["events"]!.Where(x => x.Value<int>("seq") > shownSequence))
        {
            string kind = entry.Value<string>("kind")!;
            if (kind is "user" or "assistant" or "display" or "error" or "note")
                chat.Add(new() { Kind = kind, Turn = entry.Value<int>("turn"), Label = $"第 {entry.Value<int>("turn")} 轮 · {kind switch { "user" => "你", "assistant" => "ChatVPet", "display" => "宿主展示", "note" => "人工分析", _ => "运行错误" }}",
                    Text = kind == "note" ? entry["data"]!.Value<string>("text")! : kind == "error" ? entry["data"]!.Value<string>("message")! : entry.Value<string>("title")! });
        }
        shownSequence = snapshot["events"]!.Last?.Value<int>("seq") ?? 0;
        if (follow && chat.Count != oldChatCount) ChatScroll.ScrollToEnd();
        if (shownSequence != previousSequence || previousSequence == 0)
        {
            Inspector.Update(snapshot);
        }
        if (Advanced.IsVisible) RenderAdvanced();
    }
    private static void SetText(TextBox box, string value)
    {
        if (box.Text != value) box.Text = value;
    }
    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }
    private void Advanced_Click(object sender, RoutedEventArgs e)
    {
        Advanced.SelectedIndex = int.Parse((string)((MenuItem)sender).Tag);
        Inspector.Visibility = Visibility.Collapsed;
        Advanced.Visibility = BackButton.Visibility = Visibility.Visible;
        InspectorTitle.Text = "会话工具";
        RenderAdvanced();
    }
    private void RenderAdvanced()
    {
        if (snapshot == null) return;
        int after = visibleEvents.LastOrDefault()?.Seq ?? 0;
        RenderEvents(after);
        SetText(ContextDetail, DiagnosticText.Context(snapshot));
        SetText(ConfigDetail, DiagnosticText.Config(snapshot));
    }
    private void Back_Click(object sender, RoutedEventArgs e) => ShowInspector();
    private void ShowInspector()
    {
        Inspector.Visibility = Visibility.Visible;
        Advanced.Visibility = BackButton.Visibility = Visibility.Collapsed;
        InspectorTitle.Text = "本轮测试";
    }
    private void Inspect_Click(object sender, RoutedEventArgs e)
    {
        ShowInspector();
        Inspector.SelectTurn((int)((Button)sender).Tag);
    }
    private void RenderEvents(int after = 0)
    {
        if (snapshot == null) return;
        int? seq = (EventsList.SelectedItem as EventRow)?.Seq;
        if (after == 0) visibleEvents.Clear();
        var rows = snapshot["events"]!.Where(x => x.Value<int>("seq") > after).Where(x => Filter.SelectedIndex switch
        {
            1 => x.Value<string>("kind") is "chat" or "tool" or "vision" or "summary" or "offline",
            2 => x.Value<string>("kind") is "retrieval" or "embedding" or "rerank",
            3 => x.Value<string>("kind") == "summary",
            4 => x.Value<string>("kind") is "host" or "turn" or "plugin-check",
            5 => x.Value<string>("kind") is "error" or "review" or "note", _ => true
        }).Select(x => new EventRow { Seq = x.Value<int>("seq"), Label = $"{x.Value<int>("seq"):D3} · 第{x.Value<int>("turn")}轮  {x.Value<string>("title")}", Data = (JObject)x }).ToArray();
        foreach (var row in rows) visibleEvents.Add(row);
        if (EventsList.SelectedItem == null)
            EventsList.SelectedItem = visibleEvents.FirstOrDefault(x => x.Seq == seq) ?? visibleEvents.LastOrDefault();
    }
    private void Session_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || SessionsList.SelectedItem is not SessionRow row) return;
        Run(() => Select(row.Id));
    }
    private void Select(string id) { selected = id; shownSequence = 0; chat.Clear(); snapshot = null; ShowInspector(); visibleEvents.Clear(); Detail.Clear(); Refresh(); }
    private void Event_Selected(object sender, SelectionChangedEventArgs e)
    {
        RenderDetail();
    }
    private void Raw_Click(object sender, RoutedEventArgs e) { raw = !raw; RenderDetail(); }
    private void RenderDetail()
    {
        if (EventsList.SelectedItem is not EventRow row) { if (EventsList.Items.Count == 0) Detail.Clear(); return; }
        RawButton.Content = raw ? "阅读视图" : "查看 JSON";
        Detail.TextWrapping = raw ? TextWrapping.NoWrap : TextWrapping.Wrap;
        SetText(Detail, raw ? row.Data.ToString() : DiagnosticText.Event(row.Data));
    }
    private void Bubble_Wheel(object sender, MouseWheelEventArgs e)
    {
        // Read-only chat bubbles grow with the text. The outer conversation owns scrolling.
        e.Handled = true;
        ChatScroll.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            { RoutedEvent = Mouse.MouseWheelEvent, Source = ChatScroll });
    }
    private void Defaults_Click(object sender, RoutedEventArgs e) => Run(() => new DefaultsWindow(service, this).ShowDialog());
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) RenderEvents(); }
    private void Run(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "ChatVPet Lab"); } }
    private WorkbenchSession Current => selected != null ? service.Get(selected) : throw new InvalidOperationException("请先新建或选择会话。");
    private SessionOptions? AskOptions()
    {
        var dialog = new NewSessionWindow(service) { Owner = this, Resources = Resources };
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
    private sealed class ChatRow { public int Turn { get; set; } public string Kind { get; set; } = ""; public string Label { get; set; } = ""; public string Text { get; set; } = ""; }
}
