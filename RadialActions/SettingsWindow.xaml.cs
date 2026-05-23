using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
    private readonly SettingsWindowViewModel _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        _viewModel = new SettingsWindowViewModel(Settings.Default);
        DataContext = _viewModel;
        Closed += OnClosed;
        AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(Hyperlink_RequestNavigate));
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(ActivationHotkey_PreviewKeyDown), handledEventsToo: true);
    }

    public void SelectAction(PieAction action)
    {
        if (action == null)
            return;

        _viewModel.SelectAction(action);
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
        if (e.Uri.Scheme.Equals("radialactions", StringComparison.OrdinalIgnoreCase) &&
            e.Uri.Host.Equals("licenses", StringComparison.OrdinalIgnoreCase))
        {
            OpenLicensesFile();
            e.Handled = true;
            return;
        }

        Log.Information("Opening link from Settings/Help: {Url}", e.Uri.AbsoluteUri);
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private static void OpenLicensesFile()
    {
        try
        {
            Process.Start("notepad", Path.Combine(App.MainFileInfo.DirectoryName, "Licenses.txt"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Couldn't open Licenses.txt");
            MessageBox.Show(
                $"Couldn't open Licenses.txt.\n\n{ex.Message}",
                "Licenses",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ActivationHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not TextBox { Tag: "ActivationHotkeyInput" } textBox)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.None)
            return;

        if (key is Key.Back or Key.Delete)
        {
            textBox.Clear();
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
            return;
        }

        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var hotkey = HotkeyUtil.BuildHotkeyString(key, Keyboard.Modifiers);
        if (string.IsNullOrWhiteSpace(hotkey))
            return;

        textBox.Text = hotkey;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
    }

    private void OnClosed(object sender, EventArgs e)
    {
        SaveSettings();
    }
}
