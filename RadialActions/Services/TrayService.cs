using System.IO;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace RadialActions;

internal sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _trayIcon;

    public TrayService(ResourceDictionary resources, object dataContext, string initialHotkey)
    {
        _trayIcon = (TaskbarIcon)resources["TrayIcon"];
        var trayContextMenu = (ContextMenu)resources["MainContextMenu"];
        trayContextMenu.DataContext = dataContext;
        _trayIcon.ContextMenu = trayContextMenu;
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
        _trayIcon.ShowNotification("Radial Actions", $"Press {initialHotkey} to open the menu", sound: false);
        Log.Debug("Created tray icon");
    }

    public void ShowUpdateAvailableNotification(Version latestVersion)
    {
        _trayIcon.ShowNotification(
            "Update available",
            $"Get v{latestVersion} from Settings",
            NotificationIcon.Info);
    }

    public void ShowActionFailedNotification(PieAction action, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(exception);

        var reason = exception switch
        {
            InvalidOperationException => exception.Message,
            FileNotFoundException => "Target was not found",
            DirectoryNotFoundException => "Folder was not found",
            UnauthorizedAccessException => "Access was denied",
            NotSupportedException => "Target is not supported",
            _ => "Could not launch action",
        };

        Log.Debug("Showing action failed notification for action {ActionName}: {Reason}", action.Name, reason);
        _trayIcon.ShowNotification(
            "Action failed",
            reason,
            NotificationIcon.Error);
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}
