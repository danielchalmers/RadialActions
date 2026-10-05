using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
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
    private const string OpenMenuItemTag = "OpenMenu";
    private const int GeneralTabIndex = 0;
    private const int ActionsTabIndex = 1;
    private readonly TrayService _trayService;
    private readonly HotkeyService _hotkeyService = new();
    private readonly MenuService _menuService;
    private bool _isActivationHotkeySuspended;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        Settings.Default.PropertyChanged += OnSettingsPropertyChanged;
        _trayService = new TrayService(Resources, this);
        _menuService = new MenuService(
            this,
            PieMenu,
            (Storyboard)Resources["FadeInStoryboard"],
            (Storyboard)Resources["FadeOutStoryboard"],
            (double)FindResource("MotionMenuEnterScale"));
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
            case nameof(Settings.ActivationHotkey):
                if (_isActivationHotkeySuspended)
                {
                    // The recorder still has focus, and registering now would let its next keystroke open the menu. ResumeActivationHotkey registers the new value and reports its status.
                    App.CurrentApp.ActivationHotkeyError = null;
                    break;
                }

                ApplyActivationHotkey();
                break;
        }
    }

    /// <summary>
    /// Registers the saved activation hotkey and publishes whether it works.
    /// </summary>
    private HotkeyRegistrationResult ApplyActivationHotkey()
    {
        var hotkey = Settings.Default.ActivationHotkey;
        var result = _hotkeyService.ApplyHotkey(hotkey);

        App.CurrentApp.ActivationHotkeyError = ActivationHotkeyStatus.GetError(result, hotkey);
        _trayService.UpdateToolTip(result == HotkeyRegistrationResult.Registered ? hotkey : null);

        return result;
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

    /// <summary>
    /// Opens Settings when the app is launched again while it's running, since the user is looking for it.
    /// </summary>
    public void OpenSettingsForRelaunch()
    {
        Log.Information("Launched again while running; opening Settings");
        OpenSettingsWindow(GeneralTabIndex);
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

    /// <summary>
    /// Unregisters the activation hotkey so the Settings recorder can capture the current combination instead of opening the menu.
    /// </summary>
    public void SuspendActivationHotkey()
    {
        Log.Debug("Suspending the activation hotkey while it is being recorded");
        _isActivationHotkeySuspended = true;
        _hotkeyService.ClearHotkeys();
    }

    /// <summary>
    /// Re-registers the saved activation hotkey after <see cref="SuspendActivationHotkey"/>.
    /// </summary>
    public void ResumeActivationHotkey()
    {
        Log.Debug("Resuming the activation hotkey");
        _isActivationHotkeySuspended = false;
        ApplyActivationHotkey();
    }

    /// <summary>
    /// Opens the menu, or Settings on the Actions tab when no action would appear in it.
    /// </summary>
    /// <returns>True if the menu opened.</returns>
    public bool ShowMenu(bool atCursor)
    {
        if (!MenuService.HasEnabledActions(Settings.Default.Actions))
        {
            // An empty menu would be an invisible window that takes keyboard focus.
            Log.Information("No enabled actions to show; opening the Actions tab instead of the menu");
            OpenSettingsWindow(ActionsTabIndex);
            return false;
        }

        _menuService.ShowMenu(atCursor);
        return true;
    }

    public void HideMenu(bool animate = true)
    {
        _menuService.HideMenu(animate);

        // Disarmed after the dismiss has frozen the pie's visuals, so a slice fired by releasing the hotkey keeps its release hint through the exit.
        PieMenu.IsReleaseTriggerArmed = false;
    }

    private bool ShowMenuUsingConfiguredPosition()
    {
        return ShowMenu(!Settings.Default.OpenMenuInScreenCenter);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        HideMenu(animate: false);

        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Initialize(handle, OnHotkeyPressed);
        ShowStartupNotification(ApplyActivationHotkey());

#if DEBUG
        _menuService.ShowMenu(false);
#endif

        await CheckForUpdatesAsync();
    }

    private void ShowStartupNotification(HotkeyRegistrationResult registration)
    {
        var notification = TrayNotificationText.ForStartup(
            registration,
            Settings.Default.ActivationHotkey,
            Settings.Default.HasShownWelcomeNotification,
            StartupRegistration.ForCurrentApp().IsEnabled,
            out var isWelcome);

        if (notification == null)
        {
            return;
        }

        Log.Information("Showing the startup notification <{Title}>", notification.Title);

        // A welcome the shell rejected must not count as shown, or it would never appear.
        if (!_trayService.ShowStartupNotification(notification, isWelcome) || !isWelcome)
        {
            return;
        }

        Settings.Default.HasShownWelcomeNotification = true;
        if (Settings.CanBeSaved)
        {
            Settings.Default.Save();
        }
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

        // During the exit animation the window can still be active, so a press there reopens the menu instead of being dropped.
        if (IsActive && _menuService.IsOpen)
        {
            HideMenu();
            return;
        }

        if (!ShowMenuUsingConfiguredPosition())
        {
            return;
        }

        // Arms the flick gesture: the keys are still held, so releasing them over a slice triggers it.
        PieMenu.IsReleaseTriggerArmed = Settings.Default.TriggerSliceOnHotkeyRelease;
    }

    private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray icon left clicked");
        ShowMenuUsingConfiguredPosition();
    }

    private void OnTrayKeyboardKeySelect(object sender, RoutedEventArgs e)
    {
        // Enter or Space on the focused tray icon. The pointer has nothing to do with a keyboard invocation, so open in the middle of the screen.
        Log.Debug("Tray icon selected from the keyboard");
        ShowMenu(atCursor: false);
    }

    private void OnTrayLeftMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray icon left double clicked");
        OpenSettingsWindow(ActionsTabIndex);
    }

    private void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e)
    {
        Log.Debug("Tray balloon clicked");

        if (_trayService.NotificationAction is { } failedAction)
        {
            OpenActionInSettings(failedAction);
            return;
        }

        if (_trayService.NotificationLink is { } link)
        {
            try
            {
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
            }
            catch (Win32Exception ex)
            {
                // No app is registered to open links.
                Log.Warning(ex, "Failed to open {Link} from a notification", link);
            }

            return;
        }

        // The welcome and hotkey notifications lead to the General tab, where the hotkey and Run at Windows startup are.
        OpenSettingsWindow(GeneralTabIndex);
    }

    private void OnOpenMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => OnOpenMenuItemClick(sender, e), DispatcherPriority.Normal);
            return;
        }

        // Click is raised after the popup closes, but the keyboard is still the most recent device when Enter or the access key chose the item. Like a keyboard tray invocation, that opens in the middle of the screen because the pointer can be anywhere.
        var fromKeyboard = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        Log.Debug("Open menu item selected <FromKeyboard={FromKeyboard}>", fromKeyboard);
        ShowMenu(atCursor: !fromKeyboard && !Settings.Default.OpenMenuInScreenCenter);
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

    private async void OnSliceClicked(object sender, SliceClickEventArgs e)
    {
        var slice = e.Slice;
        Log.Debug($"Slice clicked: {slice.Name}");

        if (Settings.Default.KeepMenuOpenAfterSliceClick)
        {
            // A slice has fired; releasing the still-held hotkey must not fire another one while the menu stays open.
            PieMenu.IsReleaseTriggerArmed = false;
        }
        else
        {
            // Dismiss before running the action so the fade-out starts on the same frame as the click. HideMenu also disarms the release trigger.
            HideMenu();
        }

        // Launching a process can block for hundreds of milliseconds (ShellExecute resolving a target, PowerShell starting up), which would freeze the menu mid-fade if it ran on the UI thread.
        // Failures come back here on the UI thread so the tray notification is shown from the right context.
        try
        {
            await Task.Run(slice.Execute);
        }
        catch (Exception ex)
        {
            var notification = ActionFailureMessage.Create(slice, ex);
            if (notification == null)
            {
                Log.Information(ex, $"Action was cancelled by the user: {slice.Name}");
                return;
            }

            Log.Error(ex, $"Failed to execute action: {slice.Name}");
            _trayService.ShowActionFailedNotification(slice, notification);
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
        OpenMainContextMenu(fromKeyboard: false);
    }

    private void OpenMainContextMenu(bool fromKeyboard)
    {
        var contextMenu = (ContextMenu)Resources["MainContextMenu"];
        contextMenu.DataContext = this;

        // The menu is already open, so "Open menu" would do nothing here.
        foreach (var item in contextMenu.Items.OfType<MenuItem>().Where(item => Equals(item.Tag, OpenMenuItemTag)))
        {
            item.Visibility = Visibility.Collapsed;
        }

        // From the keyboard the pointer can be anywhere, even on another monitor, so the menu opens over the pie instead.
        contextMenu.PlacementTarget = PieMenu;
        contextMenu.Placement = fromKeyboard ? PlacementMode.Center : PlacementMode.MousePoint;
        contextMenu.IsOpen = true;
    }

    private void OnSliceEditRequested(object sender, SliceClickEventArgs e)
    {
        Log.Debug($"Slice edit requested: {e.Slice.Name}");
        OpenActionInSettings(e.Slice);
        HideMenu();
    }

    private void OpenActionInSettings(PieAction action)
    {
        OpenSettingsWindow(ActionsTabIndex);
        var settingsWindow = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        settingsWindow?.SelectAction(action);
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
            OpenMainContextMenu(fromKeyboard: true);
            e.Handled = true;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (!PieMenu.IsReleaseTriggerArmed)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!HotkeyUtil.TryParse(Settings.Default.ActivationHotkey, out var modifiers, out var hotkeyKey)
            || !HotkeyUtil.IsHotkeyComponent(key, modifiers, hotkeyKey))
        {
            return;
        }

        // OnSliceClicked disarms the trigger once the dismiss has started, so the fired slice keeps its release hint through the exit.
        if (PieMenu.TriggerActiveSlice())
        {
            Log.Debug("Activation hotkey released over a slice; triggered it");
            e.Handled = true;
        }
        else
        {
            PieMenu.IsReleaseTriggerArmed = false;
            Log.Debug("Activation hotkey released with no slice hovered; menu stays open");
        }
    }

    private void FadeInStoryboard_Completed(object sender, EventArgs e)
    {
        _menuService.OnEnterCompleted();
    }

    private void FadeOutStoryboard_Completed(object sender, EventArgs e)
    {
        _menuService.OnExitCompleted();
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
