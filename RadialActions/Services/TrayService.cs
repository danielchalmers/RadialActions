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
        _trayIcon.ToolTipText = "Radial Actions";
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
        _trayIcon.ShowNotification("Radial Actions", $"Press {initialHotkey} to open the menu");
        Log.Debug("Created tray icon");
    }

    public void ShowUpdateAvailableNotification(string latestVersion)
    {
        _trayIcon.ShowNotification(
            "Update available",
            $"{latestVersion} is available to download",
            NotificationIcon.Info);
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}
