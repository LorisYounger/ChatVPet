using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;

namespace ChatVPet.Workbench;

public sealed class NewSessionWindow : Window
{
    public SessionOptions Options { get; private set; } = new();
    public NewSessionWindow(string[] configurations)
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
        Label("API 配置 · 读取 Local 目录，密钥不会出现在诊断导出中");
        var config = new ComboBox { ItemsSource = configurations, MinHeight = 30 };
        config.SelectedItem = configurations.FirstOrDefault(x => x.Contains("Qw35")) ?? configurations.FirstOrDefault(); form.Children.Add(config);
        Label("历史压缩");
        var compression = new ComboBox { ItemsSource = new[] { "少压缩 · 20 轮 / 保留 10 轮", "多压缩 · 6 轮 / 保留 2 轮", "不压缩 · 保留完整历史", "自定义 · 使用高级参数" }, SelectedIndex = 0, MinHeight = 30 }; form.Children.Add(compression);
        Label("提示词");
        var prompt = new ComboBox { ItemsSource = new[] { "跟随 API 配置", "ChatVPet 专用提示词", "完整兼容提示词" }, SelectedIndex = 0, MinHeight = 30 }; form.Children.Add(prompt);
        var rag = new CheckBox { Content = "启用知识和记忆 RAG", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 18, 0, 8) }; form.Children.Add(rag);
        var rerank = new CheckBox { Content = "启用重排 (包含工具候选)", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) }; form.Children.Add(rerank);
        var cache = new CheckBox { Content = "缓存向量与相关性分数", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) }; form.Children.Add(cache);
        var offline = new CheckBox { Content = "离线演示 · 不调用模型，仍可检查插件工具", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) }; form.Children.Add(offline);
        var editor = new TextBox { Text = JsonConvert.SerializeObject(Options, Formatting.Indented), AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 13, MinHeight = 270, MaxHeight = 400 };
        var advanced = new Button { Content = "高级参数 · 上下文预算 / 检索阈值", Padding = new Thickness(8) };
        form.Children.Add(advanced); editor.Visibility = Visibility.Collapsed; form.Children.Add(editor);
        advanced.Click += (_, _) => editor.Visibility = editor.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        confirm.Click += (_, _) =>
        {
            try
            {
                Options = JsonConvert.DeserializeObject<SessionOptions>(editor.Text)!;
                Options.Title = title.Text; Options.Config = config.SelectedItem as string ?? Options.Config;
                Options.Compression = new[] { "light", "heavy", "off", "custom" }[compression.SelectedIndex];
                Options.DedicatedPrompt = prompt.SelectedIndex == 0 ? null : prompt.SelectedIndex == 1;
                Options.Rag = rag.IsChecked == true; Options.Rerank = rerank.IsChecked == true; Options.Cache = cache.IsChecked == true; Options.Offline = offline.IsChecked == true;
                DialogResult = true;
            }
            catch (JsonException ex) { MessageBox.Show(this, ex.Message, "高级参数格式错误"); }
        };
    }
}
