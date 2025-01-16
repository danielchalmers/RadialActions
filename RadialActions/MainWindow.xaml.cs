using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using RadialActions.Properties;
using Serilog;

namespace RadialActions;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly TaskbarIcon _trayIcon;
    private HotkeyManager _hotkeyManager;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        Settings.Default.PropertyChanged += (s, e) => Dispatcher.Invoke(() => Settings_PropertyChanged(s, e));

        // Construct the tray from the resources defined.
        _trayIcon = Resources["TrayIcon"] as TaskbarIcon;
        _trayIcon.ContextMenu = Resources["MainContextMenu"] as ContextMenu;
        _trayIcon.ContextMenu.DataContext = this;
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
        Log.Debug("Created tray icon");
    }

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

    private void SetHotkey()
    {
        var registered = _hotkeyManager.RegisterHotkey(Settings.Default.ActivationHotkey);

        if (registered)
        {
            Log.Information($"Hotkey registered <{Settings.Default.ActivationHotkey}>");
        }
        else
        {
            Log.Information($"Hotkey failed to register <{Settings.Default.ActivationHotkey}>");
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        _hotkeyManager = new HotkeyManager(windowHandle);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        SetHotkey();
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        _hotkeyManager?.UnregisterHotkey(Settings.Default.ActivationHotkey);
    }

    private void OnHotkeyPressed(object sender, EventArgs e)
    {
        Activate();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        App.SetRunOnStartup(Settings.Default.RunOnStartup);
    }
}