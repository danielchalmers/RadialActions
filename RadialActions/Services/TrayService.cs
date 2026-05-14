using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace RadialActions;

internal sealed class TrayService : IDisposable
{
    private static readonly Uri LatestReleasePageUrl = new("https://github.com/danielchalmers/RadialActions/releases/latest");
    private readonly TaskbarIcon _trayIcon;
    private bool _openLatestReleaseOnNextBalloonClick;

    public TrayService(ResourceDictionary resources, object dataContext, string initialHotkey)
    {
        _trayIcon = (TaskbarIcon)resources["TrayIcon"];
        var trayContextMenu = (ContextMenu)resources["MainContextMenu"];
        trayContextMenu.DataContext = dataContext;
        _trayIcon.ContextMenu = trayContextMenu;
        _trayIcon.ToolTipText = "Radial Actions";
        _trayIcon.TrayBalloonTipClicked += OnTrayBalloonTipClicked;
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
        _trayIcon.ShowNotification("Radial Actions", $"Press {initialHotkey} to open the menu");
        Log.Debug("Created tray icon");
    }

    public void ShowUpdateAvailableNotification(Version latestVersion)
    {
        _openLatestReleaseOnNextBalloonClick = true;
        _trayIcon.ShowNotification(
            "Update available",
            $"{latestVersion} is available to download",
            NotificationIcon.Info);
    }

    private void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e)
    {
        if (!_openLatestReleaseOnNextBalloonClick)
        {
            return;
        }

        _openLatestReleaseOnNextBalloonClick = false;

        try
        {
            Process.Start(new ProcessStartInfo(LatestReleasePageUrl.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open latest release page");
        }
    }

    public void Dispose()
    {
        _trayIcon.TrayBalloonTipClicked -= OnTrayBalloonTipClicked;
        _trayIcon.Dispose();
    }
}
