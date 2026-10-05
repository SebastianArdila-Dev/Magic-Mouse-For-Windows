using Microsoft.UI.Xaml;

namespace MagicMouse.Windows.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += (_, e) => RecordFailure(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => RecordFailure(e.ExceptionObject as Exception);
        InitializeComponent();
        RequestedTheme = ApplicationTheme.Light;
    }

    private static void RecordFailure(Exception? exception)
    {
        try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup-error.txt"), exception?.ToString() ?? "Unknown startup error"); }
        catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception exception) { RecordFailure(exception); throw; }
    }
}
