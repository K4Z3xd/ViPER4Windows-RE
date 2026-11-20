using Microsoft.UI.Xaml;

namespace V4Wa.Demo;

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
                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"{DateTime.Now}\n{e.Message}\n{e.Exception}\n"
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
