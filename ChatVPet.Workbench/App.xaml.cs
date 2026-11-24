using System.IO;
using System.Windows;

namespace ChatVPet.Workbench;

public partial class App : Application
{
    public WorkbenchService Service { get; private set; } = null!;
    private LocalApi? api;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            string? root = e.Args.SkipWhile(x => x != "--root").Skip(1).FirstOrDefault();
            if (root == null)
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ChatVPet.sln"))) directory = directory.Parent;
                root = directory?.FullName ?? Environment.CurrentDirectory;
            }
            Service = new WorkbenchService(root);
            int port = int.Parse(e.Args.SkipWhile(x => x != "--port").Skip(1).FirstOrDefault() ?? "39421");
            api = new LocalApi(Service, port);
            api.Start();
            if (!e.Args.Contains("--headless"))
            {
                MainWindow = new MainWindow(Service, port);
                MainWindow.Show();
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "ChatVPet.Workbench-startup-error.txt"), ex.ToString());
            if (!e.Args.Contains("--headless")) MessageBox.Show(ex.Message, "工作台启动失败");
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        api?.Dispose();
        Service?.Dispose();
        base.OnExit(e);
    }
}
