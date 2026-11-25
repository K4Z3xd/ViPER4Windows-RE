using Microsoft.UI.Xaml;

namespace ViPER4WindowsRE;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            try
            {
                string? root = Directory.GetParent(AppContext.BaseDirectory.TrimEnd('\\', '/'))?.FullName;
                string logDir = (root != null && Directory.Exists(Path.Combine(root, "logs")))
                    ? Path.Combine(root, "logs")
                    : AppContext.BaseDirectory;
                File.WriteAllText(
                    Path.Combine(logDir, "crash.log"),
                    $"{DateTime.Now}\n{e.Message}\n{e.Exception}\n{e.Exception?.StackTrace}\n"
                );
            }
            catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
