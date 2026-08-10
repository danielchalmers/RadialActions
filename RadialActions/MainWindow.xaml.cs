using System.ComponentModel;
using System.Diagnostics;
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
    private const string FeedbackUrl = "https://github.com/danielchalmers/RadialActions/issues";
    private readonly TrayService _trayService;
    private readonly HotkeyService _hotkeyService = new();
    private readonly MenuService _menuService;

    /// <summary>
    /// True while the menu was opened by the activation hotkey and the keys have not been released
    /// yet, so releasing them over a slice triggers it (flick gesture).
    /// </summary>
    private bool _hotkeyReleasePending;

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
        _hotkeyReleasePending = false;
        _menuService.ShowMenu(atCursor);
    }

    public void HideMenu(bool animate = true)
    {
        _hotkeyReleasePending = false;
        _menuService.HideMenu(animate);
    }

    private void ShowMenuUsingConfiguredPosition()
    {
        _hotkeyReleasePending = false;
        _menuService.ShowMenu(!Settings.Default.OpenMenuInScreenCenter);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        HideMenu(animate: false);

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
            HideMenu();
        }
        else
        {
            ShowMenuUsingConfiguredPosition();
            _hotkeyReleasePending = Settings.Default.TriggerSliceOnHotkeyRelease;
        }
    }

    private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray icon left clicked");
        ShowMenuUsingConfiguredPosition();
    }

    private void OnTrayLeftMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray icon left double clicked");
        OpenSettingsWindow(1);
    }

    private void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray balloon clicked");
        OpenSettingsWindow(0);
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

    private void OnTrayFeedbackMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => OnTrayFeedbackMenuItemClick(sender, e), DispatcherPriority.Normal);
            return;
        }

        Log.Debug("Tray feedback menu item clicked");
        try
        {
            Process.Start(new ProcessStartInfo(FeedbackUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open feedback page");
        }
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

        // A slice has fired; releasing the still-held hotkey must not fire another one when the menu stays open.
        _hotkeyReleasePending = false;

        try
        {
            e.Slice.Execute();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to execute action: {e.Slice.Name}");
            _trayService.ShowActionFailedNotification(e.Slice, ex);
        }

        if (!Settings.Default.KeepMenuOpenAfterSliceClick)
        {
            HideMenu();
        }
    }

    private void OnSlicesReordered(object sender, EventArgs e)
    {
        Log.Debug("Slices reordered by dragging");
        if (Settings.CanBeSaved)
        {
            Settings.Default.Save();
        }
    }

    private void OnCenterClicked(object sender, EventArgs e)
    {
        Log.Debug("Center close target clicked");
        HideMenu();
    }

    private void OnCenterContextMenuRequested(object sender, EventArgs e)
    {
        Log.Debug("Center close target right clicked");
        OpenMainContextMenu();
    }

    private void OpenMainContextMenu()
    {
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
        HideMenu();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Log.Debug("Lost focus");
        HideMenu();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Log.Debug("Escape pressed");
            HideMenu();
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (PieMenu.HandleMenuKey(key, Keyboard.Modifiers))
        {
            e.Handled = true;
            return;
        }

        if ((key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) || key == Key.Apps)
        {
            Log.Debug("Context menu key pressed with no selected slice; opening main context menu");
            OpenMainContextMenu();
            e.Handled = true;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (!_hotkeyReleasePending)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!HotkeyUtil.TryParse(Settings.Default.ActivationHotkey, out var modifiers, out var hotkeyKey)
            || !HotkeyUtil.IsHotkeyComponent(key, modifiers, hotkeyKey))
        {
            return;
        }

        _hotkeyReleasePending = false;

        if (PieMenu.TriggerHoveredSlice())
        {
            Log.Debug("Activation hotkey released over a slice; triggered it");
            e.Handled = true;
        }
        else
        {
            Log.Debug("Activation hotkey released with no slice hovered; menu stays open");
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
