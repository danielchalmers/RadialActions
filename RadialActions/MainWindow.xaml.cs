using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using RadialActions.Properties;

namespace RadialActions;

/// <summary>
/// Main window that hosts the radial pie menu.
/// </summary>
public partial class MainWindow : Window
{
    private readonly TrayService _trayService;
    private readonly HotkeyService _hotkeyService = new();
    private readonly MenuService _menuService;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        Settings.Default.PropertyChanged += OnSettingsPropertyChanged;
        _trayService = new TrayService(Resources, this, Settings.Default.ActivationHotkey);
        _menuService = new MenuService(
            this,
            PieMenu,
            (System.Windows.Media.Animation.Storyboard)Resources["FadeInStoryboard"],
            (System.Windows.Media.Animation.Storyboard)Resources["FadeOutStoryboard"]);
    }

    /// <summary>
    /// Handles setting changes.
    /// </summary>
    private async void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(
                () => ApplySettingChange(e.PropertyName),
                DispatcherPriority.Normal);
            return;
        }

        ApplySettingChange(e.PropertyName);
    }

    private void ApplySettingChange(string propertyName)
    {
        Log.Debug("Setting changed <{PropertyName}>", propertyName);

        switch (propertyName)
        {
            case nameof(Settings.RunOnStartup):
                App.SetRunOnStartup(Settings.Default.RunOnStartup);
                break;
            case nameof(Settings.ActivationHotkey):
                _hotkeyService.ApplyHotkey(Settings.Default.ActivationHotkey);
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

    public void ShowMenu(bool atCursor)
    {
        _menuService.ShowMenu(atCursor);
    }

    public void HideMenu(bool animate = true)
    {
        _menuService.HideMenu(animate);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _menuService.HideMenu(animate: false);

        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Initialize(handle, OnHotkeyPressed);
        _hotkeyService.ApplyHotkey(Settings.Default.ActivationHotkey);

#if DEBUG
        _menuService.ShowMenu(false);
#endif

        await CheckForUpdatesAsync();
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        Settings.Default.PropertyChanged -= OnSettingsPropertyChanged;
        _hotkeyService.Dispose();
        _trayService.Dispose();
    }

    private void OnHotkeyPressed(object sender, EventArgs e)
    {
        Log.Debug("Hotkey pressed");

        if (IsActive)
        {
            _menuService.HideMenu();
        }
        else
        {
            _menuService.ShowMenu(true);
        }
    }

    private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray icon left clicked");
        _menuService.ShowMenu(true);
    }

    private void OnTrayLeftMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray icon left double clicked");
        OpenSettingsWindow(1);
    }

    private void OnTraySettingsMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => OnTraySettingsMenuItemClick(sender, e), DispatcherPriority.Normal);
            return;
        }

        var tabIndex = sender is MenuItem { Tag: string tag } && int.TryParse(tag, out var parsedIndex)
            ? parsedIndex
            : 0;

        OpenSettingsWindow(tabIndex);
    }

    private void OnTrayExitMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => OnTrayExitMenuItemClick(sender, e), DispatcherPriority.Send);
            return;
        }

        Exit();
    }

    private void OnSliceClicked(object sender, SliceClickEventArgs e)
    {
        Log.Debug($"Slice clicked: {e.Slice.Name}");
        e.Slice.Execute();
        if (!Settings.Default.KeepMenuOpenAfterSliceClick)
        {
            _menuService.HideMenu();
        }
    }

    private void OnCenterClicked(object sender, EventArgs e)
    {
        Log.Debug("Center close target clicked");
        _menuService.HideMenu();
    }

    private void OnCenterContextMenuRequested(object sender, EventArgs e)
    {
        Log.Debug("Center close target right clicked");
        var contextMenu = (ContextMenu)Resources["MainContextMenu"];
        contextMenu.DataContext = this;
        contextMenu.PlacementTarget = PieMenu;
        contextMenu.Placement = PlacementMode.MousePoint;
        contextMenu.IsOpen = true;
    }

    private void OnSliceEditRequested(object sender, SliceClickEventArgs e)
    {
        Log.Debug($"Slice edit requested: {e.Slice.Name}");
        OpenSettingsWindow(1);
        var settingsWindow = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        settingsWindow?.SelectAction(e.Slice);
        _menuService.HideMenu();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Log.Debug("Lost focus");
        _menuService.HideMenu();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Log.Debug("Escape pressed");
            _menuService.HideMenu();
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (PieMenu.HandleMenuKey(key, Keyboard.Modifiers))
        {
            e.Handled = true;
        }
    }

    private void FadeOutStoryboard_Completed(object sender, EventArgs e)
    {
        _menuService.OnFadeOutCompleted();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (!Settings.Default.CheckForUpdatesOnStartup)
        {
            Log.Debug("Startup update check skipped because {SettingName} is disabled", nameof(Settings.CheckForUpdatesOnStartup));
            return;
        }

        try
        {
            var app = App.CurrentApp;
            Log.Information("Checking for updates on startup. Current version: {CurrentVersion}", app.CurrentVersion);
            app.LatestVersion = await UpdateService.GetLatestVersion();
            app.IsUpdateAvailable = UpdateService.IsUpdateAvailable(app.CurrentVersion, app.LatestVersion);
            Log.Information(
                "Startup update check completed. Current version: {CurrentVersion}, Latest version: {LatestVersion}, Update available: {IsUpdateAvailable}",
                app.CurrentVersion,
                app.LatestVersion,
                app.IsUpdateAvailable);

            if (!app.IsUpdateAvailable || app.LatestVersion == null)
            {
                Log.Debug("No startup update notification will be shown");
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                Log.Information("Showing update available tray notification for version {LatestVersion}", app.LatestVersion);
                _trayService.ShowUpdateAvailableNotification(app.LatestVersion);
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Startup update check failed");
        }
    }
}
