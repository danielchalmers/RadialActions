using System.ComponentModel;
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
    private HotkeyManager _hotkeys;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        Settings.Default.PropertyChanged += Settings_PropertyChanged;

        // Construct the tray from the resources defined.
        _tray = Resources["TrayIcon"] as TaskbarIcon;
        _tray.ContextMenu = Resources["MainContextMenu"] as ContextMenu;
        _tray.ContextMenu.DataContext = this;
        _tray.ToolTipText = "Radial Actions";
        _tray.ForceCreate(enablesEfficiencyMode: false);
        _tray.ShowNotification("Radial Actions", "Press " + Settings.Default.ActivationHotkey + " to open the menu");
        Log.Debug("Created tray icon");
    }

    /// <summary>
    /// Handles setting changes.
    /// </summary>
    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Settings_PropertyChanged(sender, e));
            return;
        }

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
        if (!int.TryParse(tabIndex, out var index))
            index = 0;

        OpenSettingsWindow(index);
    }

    private void OpenSettingsWindow(int tabIndex)
    {
        Log.Debug($"Opening settings window to tab {tabIndex}");
        Settings.Default.SettingsTabIndex = tabIndex;
        App.ShowSingletonWindow<SettingsWindow>();
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
        _hotkeys?.UnregisterAll();
        if (!string.IsNullOrWhiteSpace(hotkey))
        {
            _hotkeys?.RegisterHotkey(hotkey);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        HideMenu();

        var handle = new WindowInteropHelper(this).Handle;
        _hotkeys = new(handle);
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        SetHotkey();

#if DEBUG
        ShowMenu(false);
#endif
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        Settings.Default.PropertyChanged -= Settings_PropertyChanged;
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

    private void OnTrayLeftMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray icon left double clicked");
        OpenSettingsWindow(1);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        App.SetRunOnStartup(Settings.Default.RunOnStartup);
    }

    private void OnSliceClicked(object sender, SliceClickEventArgs e)
    {
        Log.Debug($"Slice clicked: {e.Slice.Name}");
        e.Slice.Execute();
        if (!Settings.Default.KeepMenuOpenAfterSliceClick)
        {
            HideMenu();
        }
    }

    private void OnSliceEditRequested(object sender, SliceClickEventArgs e)
    {
        Log.Debug($"Slice edit requested: {e.Slice.Name}");
        OpenSettingsWindow(1);
        var settingsWindow = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        settingsWindow?.SelectAction(e.Slice);
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
