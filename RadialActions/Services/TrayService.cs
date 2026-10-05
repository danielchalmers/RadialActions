using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace RadialActions;

internal sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _trayIcon;

    public TrayService(ResourceDictionary resources, object dataContext)
    {
        _trayIcon = (TaskbarIcon)resources["TrayIcon"];
        var trayContextMenu = (ContextMenu)resources["MainContextMenu"];
        trayContextMenu.DataContext = dataContext;
        _trayIcon.ContextMenu = trayContextMenu;
        _trayIcon.TrayKeyboardContextMenu += OnTrayKeyboardContextMenu;
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
        Log.Debug("Created tray icon");
    }

    /// <summary>
    /// The action the most recent notification is about, so selecting the notification can open it in Settings; null when the latest one is about something else.
    /// </summary>
    public PieAction NotificationAction { get; private set; }

    public void UpdateToolTip(string registeredHotkey)
    {
        try
        {
            _trayIcon.ToolTipText = TrayNotificationText.ToolTip(registeredHotkey);
        }
        catch (InvalidOperationException ex)
        {
            // The shell rejects updates while the notification area is unavailable (for example while Explorer restarts); the icon picks up ToolTipText when it is recreated.
            Log.Warning(ex, "Couldn't update the tray icon tooltip");
        }
    }

    /// <param name="notification">The text from <see cref="TrayNotificationText.ForStartup"/>.</param>
    /// <param name="isWelcome">True for the welcome; otherwise it's a hotkey problem and gets a warning icon.</param>
    /// <returns>False when the shell couldn't show it.</returns>
    public bool ShowStartupNotification(TrayNotification notification, bool isWelcome)
    {
        return Show(notification, isWelcome ? NotificationIcon.None : NotificationIcon.Warning, sound: false);
    }

    public void ShowUpdateAvailableNotification(Version latestVersion)
    {
        Show(TrayNotificationText.UpdateAvailable(latestVersion), NotificationIcon.Info);
    }

    /// <param name="notification">The text from <see cref="ActionFailureMessage.Create"/>.</param>
    public void ShowActionFailedNotification(PieAction action, TrayNotification notification)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Show(notification, NotificationIcon.Error))
        {
            NotificationAction = action;
        }
    }

    public void Dispose()
    {
        _trayIcon.TrayKeyboardContextMenu -= OnTrayKeyboardContextMenu;
        _trayIcon.Dispose();
    }

    private bool Show(TrayNotification notification, NotificationIcon icon, bool sound = true)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            _trayIcon.ShowNotification(notification.Title, notification.Message, icon, sound: sound);
        }
        catch (InvalidOperationException ex)
        {
            // The shell rejects it while the notification area is unavailable, like the tooltip (ObjectDisposedException after disposal is also an InvalidOperationException); an earlier notification still on screen keeps its action.
            Log.Warning(ex, "Couldn't show the tray notification <{Title}>", notification.Title);
            return false;
        }

        NotificationAction = null;
        return true;
    }

    private void OnTrayKeyboardContextMenu(object sender, RoutedEventArgs e)
    {
        // Shift+F10 or the Menu key on the focused tray icon. The shell also sends this after a mouse right-click, which has already opened the menu at the pointer.
        if (_trayIcon.ContextMenu is not { IsOpen: false })
        {
            return;
        }

        Log.Debug("Tray icon context menu requested from the keyboard");
        _trayIcon.ShowContextMenu(TaskbarIcon.GetPopupTrayPosition());
    }
}
