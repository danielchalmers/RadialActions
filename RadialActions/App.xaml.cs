using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace RadialActions;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, INotifyPropertyChanged
{
    /// <summary>
    /// The main executable file of the application.
    /// </summary>
    public static FileInfo MainFileInfo { get; } = new(Environment.ProcessPath);
    public static App CurrentApp => (App)Current;

    private Version _latestVersion;
    private bool _isUpdateAvailable;

    public event PropertyChangedEventHandler PropertyChanged;

    public Version CurrentVersion { get; } =
        Version.TryParse(FileVersionInfo.GetVersionInfo(MainFileInfo.FullName)?.FileVersion, out var parsedVersion)
            ? parsedVersion
            : null;

    public Version LatestVersion
    {
        get => _latestVersion;
        set => SetProperty(ref _latestVersion, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Verbose()
            .WriteTo.Debug()
            .CreateLogger();

        Log.Information($"Starting Radial Actions {FileVersionInfo.GetVersionInfo(MainFileInfo.FullName).FileVersion}");
        Log.Information($"Runtime: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets or deletes a value in the registry which enables the current executable to run on system startup.
    /// </summary>
    public static bool SetRunOnStartup(bool enable)
    {
        var keyName = "RadialActions";
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        if (key == null)
        {
            Log.Error("Failed to open startup registry key");
            return false;
        }

        try
        {
            if (enable)
            {
                Log.Information($"Setting to run on startup under key named {keyName}");
                key.SetValue(keyName, MainFileInfo.FullName);
            }
            else
            {
                Log.Information($"Removing from startup under key named {keyName}");
                key.DeleteValue(keyName, false);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set run on startup");
            return false;
        }
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

        // Restore an existing window.
        if (window.IsVisible)
        {
            SystemCommands.RestoreWindow(window);
            window.Activate();
            return;
        }

        // Show the new window.
        window.Show();
    }
}
