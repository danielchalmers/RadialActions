using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;

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

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}
