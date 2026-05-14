using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace RadialActions;

internal sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _trayIcon;
    private Uri _latestReleaseUrl;

    public TrayService(ResourceDictionary resources, object dataContext, string initialHotkey)
    {
        _trayIcon = (TaskbarIcon)resources["TrayIcon"];
        var trayContextMenu = (ContextMenu)resources["MainContextMenu"];
        trayContextMenu.DataContext = dataContext;
        _trayIcon.ContextMenu = trayContextMenu;
        _trayIcon.ToolTipText = "Radial Actions\nLeft click: Open menu\nDouble click: Open actions settings\nRight click: Open tray menu";
        _trayIcon.TrayBalloonTipClicked += OnTrayBalloonTipClicked;
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
        _trayIcon.ShowNotification("Radial Actions", $"Press {initialHotkey} to open the menu");
        Log.Debug("Created tray icon");
    }

    public void ShowUpdateAvailableNotification(Version latestVersion, Uri latestReleaseUrl)
    {
        _latestReleaseUrl = latestReleaseUrl;
        _trayIcon.ShowNotification(
            "Update available",
            $"{latestVersion} is available to download",
            NotificationIcon.Info);
    }

    private void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e)
    {
        if (_latestReleaseUrl == null)
        {
            return;
        }

        try
        {
            Log.Information("Opening update release URL from tray notification: {Url}", _latestReleaseUrl.AbsoluteUri);
            Process.Start(new ProcessStartInfo(_latestReleaseUrl.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open update release URL");
        }
    }

    public void Dispose()
    {
        _trayIcon.TrayBalloonTipClicked -= OnTrayBalloonTipClicked;
        _trayIcon.Dispose();
    }
}
