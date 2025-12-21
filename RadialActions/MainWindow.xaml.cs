using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using RadialActions.Properties;

namespace RadialActions;

/// <summary>
/// Main window that hosts the radial pie menu.
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
        _tray.ShowNotification(App.FriendlyName, "Press " + Settings.Default.ActivationHotkey + " to open the menu");
        Log.Debug("Created tray icon");
    }

    /// <summary>
    /// Handles setting changes.
    /// </summary>
    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        Log.Debug($"Setting changed <{e.PropertyName}>");

        switch (e.PropertyName)
        {
            case nameof(Settings.ActivationHotkey):
                SetHotkey();
                break;
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

        // If it doesn't even exist then it's probably somewhere that requires special access.
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
            MessageBox.Show(this,
                "Couldn't open settings file.\n\n" +
                "This app may have been reuploaded without permission. If you paid for it, ask for a refund and download it for free from the original source: https://github.com/danielchalmers/RadialActions.\n\n" +
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

    private void SetHotkey()
    {
        var hotkey = Settings.Default.ActivationHotkey;
        if (!string.IsNullOrWhiteSpace(hotkey))
        {
            _hotkeys?.RegisterHotkey(hotkey);
        }
    }

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
        _hotkeys?.Dispose();
    }

    /// <summary>
    /// Shows the radial menu.
    /// </summary>
    /// <param name="atCursor">If true, centers on cursor; otherwise centers on screen.</param>
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

    private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray icon left clicked");
        ShowMenu(true);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        App.SetRunOnStartup(Settings.Default.RunOnStartup);
    }

    private void OnSliceClicked(object sender, SliceClickEventArgs e)
    {
        Log.Debug($"Slice clicked: {e.Slice.Name}");
        e.Slice.Execute();
        HideMenu();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Log.Debug("Lost focus");
        HideMenu();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Log.Debug("Escape pressed");
            HideMenu();
            e.Handled = true;
        }
    }
}
