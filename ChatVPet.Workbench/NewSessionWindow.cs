using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;

namespace ChatVPet.Workbench;

public sealed class NewSessionWindow : Window
{
    public SessionOptions Options { get; private set; } = new();
    public NewSessionWindow(WorkbenchService service)
    {
        Title = "新建测试会话"; Width = 560; Height = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(29, 32, 38)); Foreground = Brushes.White;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var confirm = new Button { Content = "创建并开始", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 18, 0, 0) };
        DockPanel.SetDock(confirm, Dock.Bottom); root.Children.Add(confirm);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; root.Children.Add(scroll);
        var form = new StackPanel(); scroll.Content = form;
        void Label(string value) => form.Children.Add(new TextBlock { Text = value, Margin = new Thickness(0, 13, 0, 7), TextWrapping = TextWrapping.Wrap });
        Label("会话名称");
        var title = new TextBox { Text = "新测试会话" }; form.Children.Add(title);
        var data = new DataSettingsForm(this, service);
        form.Children.Add(new Expander { Header = "API 与桌宠数据 · 使用全局设置", Content = data, Foreground = Foreground, Margin = new Thickness(0, 12, 0, 4) });
        Label("历史压缩");
        var compression = new ComboBox { ItemsSource = new[] { "少压缩 · 20 轮 / 保留 10 轮", "多压缩 · 6 轮 / 保留 2 轮", "不压缩 · 保留完整历史", "自定义 · 使用高级参数" }, SelectedIndex = 0, MinHeight = 30 }; form.Children.Add(compression);
        var options = new StackPanel();
        options.Children.Add(DataSettingsForm.Label("提示词"));
        var prompt = new ComboBox { ItemsSource = new[] { "跟随 API 配置", "ChatVPet 专用提示词", "完整兼容提示词" }, SelectedIndex = 0, MinHeight = 30 }; options.Children.Add(prompt);
        var rag = new CheckBox { Content = "启用知识和记忆 RAG", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 18, 0, 8) }; options.Children.Add(rag);
        var rerank = new CheckBox { Content = "启用重排 (包含工具候选)", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) }; options.Children.Add(rerank);
        var cache = new CheckBox { Content = "缓存向量与相关性分数", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) }; options.Children.Add(cache);
        var offline = new CheckBox { Content = "离线演示 · 不调用模型，仍可检查插件工具", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) }; form.Children.Add(offline);
        var fields = options;
        var reads = new List<Action>();
        void Number(string label, int current, Action<int> set)
        {
            fields.Children.Add(DataSettingsForm.Label(label));
            var box = new TextBox { Text = current.ToString() }; fields.Children.Add(box);
            reads.Add(() =>
            {
                if (!int.TryParse(box.Text, out int value) || value < 0) throw new FormatException(label + "需要非负整数。");
                set(value);
            });
        }
        Number("自定义压缩起始轮数", Options.HistoryThreshold, x => Options.HistoryThreshold = x);
        Number("自定义保留轮数", Options.KeepRecent, x => Options.KeepRecent = x);
        Number("自定义历史字符预算", Options.HistoryCharacters, x => Options.HistoryCharacters = x);
        Number("参考资料字符预算", Options.ReferenceCharacters, x => Options.ReferenceCharacters = x);
        Number("摘要字符预算", Options.SummaryCharacters, x => Options.SummaryCharacters = x);
        Number("请求字符上限", Options.RequestCharacters, x => Options.RequestCharacters = x);
        fields.Children.Add(DataSettingsForm.Label("最低重排分数（0 至 1）"));
        var score = new TextBox { Text = Options.MinimumRerankScore.ToString(CultureInfo.CurrentCulture) }; fields.Children.Add(score);
        form.Children.Add(new Expander { Header = "高级参数", Content = fields, Foreground = Foreground });
        confirm.Click += (_, _) =>
        {
            try
            {
                foreach (var read in reads) read();
                if (!double.TryParse(score.Text, out var minimum) || !double.IsFinite(minimum) || minimum < 0 || minimum > 1)
                    throw new FormatException("最低重排分数需要在 0 至 1 之间。");
                Options.MinimumRerankScore = minimum;
                var sources = data.Values;
                Options.Title = title.Text; Options.Config = sources.Config;
                Options.ModDirectory = sources.ModDirectory; Options.SaveFile = sources.SaveFile;
                Options.Compression = new[] { "light", "heavy", "off", "custom" }[compression.SelectedIndex];
                Options.DedicatedPrompt = prompt.SelectedIndex == 0 ? null : prompt.SelectedIndex == 1;
                Options.Rag = rag.IsChecked == true; Options.Rerank = rerank.IsChecked == true; Options.Cache = cache.IsChecked == true; Options.Offline = offline.IsChecked == true;
                DialogResult = true;
            }
            catch (FormatException ex) { MessageBox.Show(this, ex.Message, "高级参数格式错误"); }
        };
    }
}
