using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RadialActions;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
[INotifyPropertyChanged]
public partial class App : Application
{
    // Debug builds use their own name so they can run next to an installed copy.
#if DEBUG
    private const string SingleInstanceName = @"Local\RadialActions.Debug";
#else
    private const string SingleInstanceName = @"Local\RadialActions";
#endif

    private SingleInstance _singleInstance;

    /// <summary>
    /// The main executable file of the application.
    /// </summary>
    public static FileInfo MainFileInfo { get; } = new(Environment.ProcessPath);
    public static App CurrentApp => (App)Current;

    [ObservableProperty]
    private Version _latestVersion;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    /// <summary>
    /// Why the activation hotkey isn't working (empty, unrecognized, or taken by another app), or null when it registered.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivationHotkeyError))]
    private string _activationHotkeyError;

    public bool HasActivationHotkeyError => !string.IsNullOrEmpty(ActivationHotkeyError);

    public Version CurrentVersion { get; } =
        Version.TryParse(FileVersionInfo.GetVersionInfo(MainFileInfo.FullName)?.FileVersion, out var parsedVersion)
            ? parsedVersion
            : null;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Debug()
            .CreateLogger();

        Log.Information($"Starting Radial Actions {FileVersionInfo.GetVersionInfo(MainFileInfo.FullName).FileVersion}");
        Log.Information($"Runtime: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");

        // Only the first copy builds the main window, since a second copy's tray icon would replace the running copy's and its hotkey would fail.
        _singleInstance = new SingleInstance(SingleInstanceName);
        if (!_singleInstance.IsFirst)
        {
            Log.Information("Radial Actions is already running; asking that copy to open Settings");
            _singleInstance.RequestShow();
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow();
        _singleInstance.ListenForShowRequests(() => mainWindow.Dispatcher.InvokeAsync(mainWindow.OpenSettingsForRelaunch));
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Shows a singleton window of the specified type.
    /// If the window is already open, it activates the existing window.
    /// Otherwise, it creates and shows a new instance of the window.
    /// </summary>
    /// <typeparam name="T">The type of the window to show.</typeparam>
    /// <param name="owner">Optional owner window for the singleton window.</param>
    public static void ShowSingletonWindow<T>(Window owner = null) where T : Window, new()
    {
        var window = Current.Windows.OfType<T>().FirstOrDefault() ?? new T();
        window.Owner = owner;

        window.Show();

        if (window.WindowState == WindowState.Minimized)
        {
            SystemCommands.RestoreWindow(window);
        }

        window.Activate();
    }
}
