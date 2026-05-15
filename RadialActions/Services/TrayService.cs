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

    public void ShowActionFailedNotification(string actionName, string reason)
    {
        var title = string.IsNullOrWhiteSpace(actionName)
            ? "Action failed"
            : $"Action failed: {actionName}";

        _trayIcon.ShowNotification(
            title,
            reason,
            NotificationIcon.Error);
    }

    internal static string GetActionFailureReason(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            InvalidOperationException => exception.Message,
            FileNotFoundException => "Target was not found",
            DirectoryNotFoundException => "Folder was not found",
            UnauthorizedAccessException => "Access was denied",
            NotSupportedException => "Target is not supported",
            _ => "Could not launch action",
        };
    }

    public void ShowActionFailedNotification(string actionName, Exception exception)
    {
        ShowActionFailedNotification(actionName, GetActionFailureReason(exception));
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}
