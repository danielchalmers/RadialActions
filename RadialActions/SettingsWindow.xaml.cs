using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using RadialActions.Properties;

namespace RadialActions;

/// <summary>
/// Settings window for configuring the application.
/// </summary>
public partial class SettingsWindow : Window
{
    private const string ShortcutInputTag = "HotkeyInput";

    private readonly SettingsWindowViewModel _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();

        // The default size can be taller than the work area on small or highly scaled displays, and WPF doesn't clamp it.
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width);
        Height = Math.Min(Height, workArea.Height);

        _viewModel = new SettingsWindowViewModel(Settings.Default);
        DataContext = _viewModel;
        Closed += OnClosed;
        AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(Hyperlink_RequestNavigate));
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(ShortcutInput_PreviewKeyDown), handledEventsToo: true);
    }

    public void SelectAction(PieAction action)
    {
        if (action == null)
            return;

        _viewModel.SelectAction(action);
        ActionsView.FocusSelectedAction();
    }

    private void SaveSettings()
    {
        if (Settings.CanBeSaved)
        {
            Settings.Default.Save();
            Log.Information("Settings saved");
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private void DownloadUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(UpdateService.LatestReleasePageUrl);
    }

    private static void OpenUrl(string url)
    {
        Log.Information("Opening link from Settings: {Url}", url);
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void UpdateBanner_TargetUpdated(object sender, DataTransferEventArgs e)
    {
        // The update check can finish while Settings is open; announce the banner then, since it appears without focus moving to it.
        if (e.Property != VisibilityProperty || !IsLoaded || UpdateBanner.Visibility != Visibility.Visible)
            return;

        LiveRegionAnnouncer.AnnounceAfterLayout(UpdateBannerMessage);
    }

    private void ShortcutInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // The action editor's custom shortcut box records any combination; the activation hotkey has its own recorder button on the General tab.
        if (e.OriginalSource is not TextBox { Tag: ShortcutInputTag } textBox)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (key == Key.None || HotkeyUtil.IsRecorderPassThroughKey(key, modifiers, isActivationHotkey: false))
            return;

        if (key is Key.Back or Key.Delete)
        {
            textBox.Clear();
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
            return;
        }

        if (HotkeyUtil.IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var hotkey = HotkeyUtil.BuildHotkeyString(key, modifiers);
        if (string.IsNullOrWhiteSpace(hotkey))
            return;

        textBox.Text = hotkey;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        textBox.CaretIndex = textBox.Text.Length;
        e.Handled = true;
    }

    private void OnClosed(object sender, EventArgs e)
    {
        SaveSettings();
    }
}
