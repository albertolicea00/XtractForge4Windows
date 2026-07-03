using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using XtractForge.Core.Engine;
using XtractForge.Services;

namespace XtractForge;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static SettingsService Settings { get; } = new();
    public static DownloadManager Manager { get; } = new(() => Settings.Current);
    public static IntakeService Intake { get; } = new(Manager);

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Single instance: redirect any second launch to the first one.
        var mainInstance = AppInstance.FindOrRegisterForKey("main");
        if (!mainInstance.IsCurrent)
        {
            var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            mainInstance.RedirectActivationToAsync(activationArgs).AsTask().Wait();
            Environment.Exit(0);
            return;
        }
        mainInstance.Activated += OnRedirectedActivation;

        MainWindow = new MainWindow();
        ThemeService.Apply(MainWindow, Settings.Current.Appearance);
        MainWindow.Activate();
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments args)
    {
        // A second launch (or, later, an xtractforge:// activation) lands here.
        MainWindow?.DispatcherQueue.TryEnqueue(() => MainWindow.BringToFront());
    }
}
