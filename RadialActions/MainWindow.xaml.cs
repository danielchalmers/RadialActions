using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
    private bool _isFadingOut;

    private Storyboard FadeInStoryboard => (Storyboard)Resources["FadeInStoryboard"];
    private Storyboard FadeOutStoryboard => (Storyboard)Resources["FadeOutStoryboard"];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        Settings.Default.PropertyChanged += OnSettingsPropertyChanged;

        _tray = (TaskbarIcon)Resources["TrayIcon"];
        var trayContextMenu = (ContextMenu)Resources["MainContextMenu"];
        trayContextMenu.DataContext = this;
        _tray.ContextMenu = trayContextMenu;
        _tray.ToolTipText = "Radial Actions";
        _tray.ForceCreate(enablesEfficiencyMode: false);
        _tray.ShowNotification("Radial Actions", $"Press {Settings.Default.ActivationHotkey} to open the menu");
        Log.Debug("Created tray icon");
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
        if (_hotkeys is null)
        {
            return;
        }

        var hotkey = Settings.Default.ActivationHotkey;
        _hotkeys.UnregisterAll();

        if (!string.IsNullOrWhiteSpace(hotkey))
        {
            _hotkeys.RegisterHotkey(hotkey);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        HideMenu(animate: false);

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
        Settings.Default.PropertyChanged -= OnSettingsPropertyChanged;

        if (_hotkeys is not null)
        {
            _hotkeys.HotkeyPressed -= OnHotkeyPressed;
            _hotkeys.Dispose();
            _hotkeys = null;
        }
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

        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }

        Activate();
        FocusMenuForKeyboardInput();
        PieMenu.ResetInputState();
        IsHitTestVisible = true;
        BeginFadeIn();
    }

    public void HideMenu(bool animate = true)
    {
        if (!IsVisible || _isFadingOut)
        {
            return;
        }

        Log.Information("Dismissing menu");

        if (!animate || IsReducedMotionEnabled())
        {
            HideMenuImmediately();
            return;
        }

        BeginFadeOut();
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

    private void OnSliceClicked(object sender, SliceClickEventArgs e)
    {
        Log.Debug($"Slice clicked: {e.Slice.Name}");
        e.Slice.Execute();
        if (!Settings.Default.KeepMenuOpenAfterSliceClick)
        {
            HideMenu();
        }
    }

    private void OnCenterClicked(object sender, EventArgs e)
    {
        Log.Debug("Center close target clicked");
        HideMenu();
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
        }
    }

    private async void FocusMenuForKeyboardInput()
    {
        if (!IsVisible)
        {
            return;
        }

        Focus();
        Keyboard.Focus(PieMenu);

        await Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible)
            {
                return;
            }

            Activate();
            Focus();
            Keyboard.Focus(PieMenu);
        }, DispatcherPriority.Input);
    }

    private void BeginFadeIn()
    {
        StopFadeAnimations();

        if (IsReducedMotionEnabled())
        {
            _isFadingOut = false;
            Opacity = 1;
            return;
        }

        _isFadingOut = false;
        FadeInStoryboard.Begin(this, HandoffBehavior.SnapshotAndReplace, true);
    }

    private void BeginFadeOut()
    {
        StopFadeAnimations();

        if (IsReducedMotionEnabled())
        {
            HideMenuImmediately();
            return;
        }

        _isFadingOut = true;
        IsHitTestVisible = false;
        FadeOutStoryboard.Begin(this, HandoffBehavior.SnapshotAndReplace, true);
    }

    private void StopFadeAnimations()
    {
        FadeInStoryboard.Remove(this);
        FadeOutStoryboard.Remove(this);
    }

    private void FadeOutStoryboard_Completed(object sender, EventArgs e)
    {
        if (!_isFadingOut)
        {
            return;
        }

        HideMenuImmediately();
    }

    private static bool IsReducedMotionEnabled()
    {
        return !SystemParameters.ClientAreaAnimation;
    }

    private void HideMenuImmediately()
    {
        _isFadingOut = false;
        StopFadeAnimations();
        IsHitTestVisible = false;
        Opacity = 0;
        Hide();
    }
}
