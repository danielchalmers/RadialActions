using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using RadialActions.Properties;
using static RadialActions.InteractivePie;

namespace RadialActions;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly TaskbarIcon _tray;
    private IntPtr _handle;
    private HotkeyManager _hotkeys;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        Settings.Default.PropertyChanged += (s, e) => Dispatcher.Invoke(() => Settings_PropertyChanged(s, e));

        // Construct the tray from the resources defined.
        _tray = Resources["TrayIcon"] as TaskbarIcon;
        _tray.ContextMenu = Resources["MainContextMenu"] as ContextMenu;
        _tray.ContextMenu.DataContext = this;
        _tray.ForceCreate(enablesEfficiencyMode: false);
        _tray.ShowNotification("Radial Actions", "Running in the background");
        Log.Debug("Created tray icon");

        // Temporary
        Slices.Add(new("PlayPause"));
        Slices.Add(new("Test Slice 1"));
        Slices.Add(new("Test Slice 2"));
        Slices.Add(new("Test Slice 3"));
        Slices.Add(new("Test Slice 4"));
    }

    public ObservableCollection<Slice> Slices { get; set; } = [];

    /// <summary>
    /// Handles setting changes.
    /// </summary>
    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        Log.Debug($"Setting changed <{e.PropertyName}>");

        if (e.PropertyName == nameof(Settings.ActivationHotkey))
        {
            SetHotkey();
        }
    }

    /// <summary>
    /// Opens a new settings window or activates the existing one.
    /// </summary>
    [RelayCommand]
    public void OpenSettingsWindow(string tabIndex)
    {
        Log.Debug($"Opening settings window to tab {tabIndex}");
        Settings.Default.SettingsTabIndex = int.Parse(tabIndex);
        App.ShowSingletonWindow<SettingsWindow>(this);
    }
    /// <summary>
    /// Opens the settings file in Notepad.
    /// </summary>
    [RelayCommand]
    public void OpenSettingsFile()
    {
        // Save first if we can so it's up-to-date.
        if (Settings.CanBeSaved)
        {
            Settings.Default.Save();
        }

        // If it doesn't even exist then it's probably somewhere that requires special access and we shouldn't even be at this point.
        if (!Settings.Exists)
        {
            MessageBox.Show(this,
                "Settings file doesn't exist and couldn't be created.",
                Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Open settings file in notepad.
        try
        {
            Process.Start("notepad", Settings.FilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Couldn't open notepad");
            // Lazy scammers on the Microsoft Store may reupload without realizing it gets sandboxed, making it unable to start the Notepad process (#1, #12).
            MessageBox.Show(this,
                "Couldn't open settings file.\n\n" +
                "This app may have be reuploaded without permission. If you paid for it, ask for a refund and download it for free from the original source: https://github.com/danielchalmers/RadialActions.\n\n" +
                $"If it still doesn't work, create a new Issue at that link with details on what happened and include this error: \"{ex.Message}\"",
                Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Closes the app.
    /// </summary>
    [RelayCommand]
    public void Exit()
    {
        Application.Current.Shutdown();
    }

    private void SetHotkey() => _hotkeys.RegisterHotkey(Settings.Default.ActivationHotkey);

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        HideMenu();

        _handle = new WindowInteropHelper(this).Handle;
        _hotkeys = new(_handle);
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        SetHotkey();

#if DEBUG
        ShowMenu(false);
#endif
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        _hotkeys?.UnregisterHotkey(Settings.Default.ActivationHotkey);
    }

    public void ShowMenu(bool atCursor)
    {
        if (atCursor)
        {
            Log.Information("Opening at the cursor");
            this.CenterOnCursor();
        }
        else
        {
            Log.Information("Opening at the center of the screen");
            this.CenterOnScreen();
        }

        Show();
        Activate();
    }

    public void HideMenu()
    {
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        Log.Information("Dismissing menu");
        Hide();
    }

    private void OnHotkeyPressed(object sender, EventArgs e)
    {
        Log.Debug("Hotkey pressed");

        if (IsActive)
        {
            HideMenu();
        }
        else
        {
            ShowMenu(true);
        }
    }

    private void OnTrayLeftMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray icon double clicked");
        ShowMenu(false);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        App.SetRunOnStartup(Settings.Default.RunOnStartup);
    }

    private void OnSliceClicked(object sender, SliceClickEventArgs e)
    {
        Log.Debug($"Took a byte out of slice {e.Slice.Name}");

        switch (e.Slice.Name)
        {
            case "PlayPause":
                Actions.SimulateKey(0xB3); // VK_MEDIA_PLAY_PAUSE
                break;

            default:
                Log.Error("This slice has not been implemented");
                break;
        }

        HideMenu();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Log.Debug("Lost focus");
        HideMenu();
    }
}