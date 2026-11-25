using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using VPet.Plugin.ChatVPet;

namespace ChatVPet.Workbench;

internal sealed class DataSettingsForm : StackPanel
{
    private readonly ComboBox config = new() { MinHeight = 30 };
    private readonly TextBox mod = new();
    private readonly TextBox save = new();
    public WorkbenchDefaults Values => new()
    {
        Config = config.SelectedItem as string ?? "", ModDirectory = mod.Text.Trim(), SaveFile = save.Text.Trim()
    };

    public DataSettingsForm(Window owner, WorkbenchService service, Action<string>? selectConfig = null)
    {
        Children.Add(Label("API 配置"));
        config.ItemsSource = service.Configurations();
        config.SelectedItem = service.Defaults.Config;
        config.SelectionChanged += (_, _) => selectConfig?.Invoke(config.SelectedItem as string ?? "");
        Children.Add(config);
        AddPath("MOD 目录", mod, service.Defaults.ModDirectory, () =>
        {
            var dialog = new OpenFolderDialog { Title = "选择具体的 VPet MOD 目录，例如 0000_core" };
            return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
        });
        AddPath("VPet 存档", save, service.Defaults.SaveFile, () =>
        {
            var dialog = new OpenFileDialog { Title = "选择 VPet 存档或备份", Filter = "VPet 存档 (*.lps)|*.lps|所有文件|*.*" };
            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        });
        Children.Add(Label("MOD 留空使用测试商品；存档留空使用测试员和 10 元余额。"));
    }

    public static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 5) };

    private void AddPath(string label, TextBox field, string value, Func<string?> pick)
    {
        Children.Add(Label(label));
        var row = new DockPanel();
        var browse = new Button { Content = "浏览…" };
        browse.Click += (_, _) => { if (pick() is { } path) field.Text = path; };
        DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
        field.Text = value; row.Children.Add(field); Children.Add(row);
    }
}

internal sealed class DefaultsWindow : Window
{
    public DefaultsWindow(WorkbenchService service, Window owner)
    {
        Title = "全局设置"; Width = 590; Height = 700; Owner = owner; Resources = owner.Resources;
        Background = owner.Background; Foreground = owner.Foreground; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(22) }; Content = root;
        var ok = new Button { Content = "保存全局设置" };
        DockPanel.SetDock(ok, Dock.Bottom); root.Children.Add(ok);
        var form = new StackPanel();
        root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        form.Children.Add(DataSettingsForm.Label("用于新建会话和测试集。每个会话保存自己的 API、MOD 数据和存档副本。"));
        var editor = new StackPanel();
        OpenAIChatConfig profile = new();
        JObject original = new();
        var readFields = new List<Action>();
        void EditProfile(string name)
        {
            editor.Children.Clear(); readFields.Clear();
            profile = string.IsNullOrEmpty(name) ? new() : service.ReadConfig(name);
            original = JObject.FromObject(profile);
            void Field(string label, string value, Action<string> update, bool secret = false)
            {
                editor.Children.Add(DataSettingsForm.Label(label));
                if (secret)
                {
                    var box = new PasswordBox { Password = value, Padding = new Thickness(8) };
                    editor.Children.Add(box); readFields.Add(() => update(box.Password));
                }
                else
                {
                    var box = new TextBox { Text = value }; editor.Children.Add(box); readFields.Add(() => update(box.Text.Trim()));
                }
            }
            Field("对话 API 地址", profile.ApiUrl, s => profile.ApiUrl = s);
            Field("对话模型", profile.Model, s => profile.Model = s);
            Field("对话 API Key", profile.ApiKey, s => profile.ApiKey = s, true);
            Field("操作 / 视觉 / 摘要 API 地址", profile.ToolApiUrl, s => profile.ToolApiUrl = s);
            Field("操作模型", profile.ToolModel, s => profile.ToolModel = s);
            Field("操作 API Key", profile.ToolApiKey, s => profile.ToolApiKey = s, true);
            Field("向量 API 地址", profile.EmbeddingApiUrl, s => profile.EmbeddingApiUrl = s);
            Field("向量模型", profile.EmbeddingModel, s => profile.EmbeddingModel = s);
            Field("向量 API Key", profile.EmbeddingApiKey, s => profile.EmbeddingApiKey = s, true);
            Field("重排评分地址（完整端点）", profile.RerankApiUrl, s => profile.RerankApiUrl = s);
            Field("重排模型", profile.RerankModel, s => profile.RerankModel = s);
            Field("重排 API Key", profile.RerankApiKey, s => profile.RerankApiKey = s, true);
            var enabled = new CheckBox { Content = "启用重排", Foreground = Foreground, IsChecked = profile.RerankEnabled };
            editor.Children.Add(enabled); readFields.Add(() => profile.RerankEnabled = enabled.IsChecked == true);
        }
        var data = new DataSettingsForm(this, service, EditProfile); form.Children.Add(data);
        EditProfile(data.Values.Config);
        form.Children.Add(new Expander { Header = "编辑通用 API 地址、模型与密钥", Foreground = Foreground, Content = editor, Margin = new Thickness(0, 12, 0, 6) });
        ok.Click += (_, _) =>
        {
            try
            {
                var value = data.Values;
                if (value.ModDirectory.Length > 0 && !Directory.Exists(value.ModDirectory)) throw new DirectoryNotFoundException("找不到所选 MOD 目录。");
                if (value.SaveFile.Length > 0 && !File.Exists(value.SaveFile)) throw new FileNotFoundException("找不到所选存档。");
                foreach (var read in readFields) read();
                service.SaveDefaults(value, value.Config.Length == 0 || !JToken.DeepEquals(original, JObject.FromObject(profile)) ? profile : null);
                DialogResult = true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Title); }
        };
    }
}
