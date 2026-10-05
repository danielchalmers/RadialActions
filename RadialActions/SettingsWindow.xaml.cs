using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private const string LatestReleaseUrl = "https://github.com/danielchalmers/RadialActions/releases/latest";
    private const string ActivationHotkeyInputTag = "ActivationHotkeyInput";
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
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(HotkeyInput_PreviewKeyDown), handledEventsToo: true);
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(ActivationHotkeyInput_TextChanged));
        CommandManager.AddPreviewExecutedHandler(this, ActivationHotkeyInput_PreviewExecuted);
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
        OpenUrl(LatestReleaseUrl);
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

    private void HotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not TextBox { Tag: ActivationHotkeyInputTag or ShortcutInputTag } textBox)
            return;

        var isActivationHotkey = textBox.Tag is ActivationHotkeyInputTag;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (key == Key.None || HotkeyUtil.IsRecorderPassThroughKey(key, modifiers, isActivationHotkey))
            return;

        if (isActivationHotkey)
        {
            // A held key would otherwise record again, or repeat its hint to screen readers, on every auto-repeat.
            if (e.IsRepeat)
            {
                e.Handled = true;
                return;
            }

            // The box only mirrors the setting through a one-way binding. The view model writes it, and only for combinations that are safe to register globally, so every other key is swallowed here instead of reaching the box as text.
            if (key is Key.Back or Key.Delete)
                _viewModel.General.ClearActivationHotkey();
            else
                _viewModel.General.RecordActivationHotkey(modifiers, key);

            ShowActivationHotkey(textBox);
            e.Handled = true;
            return;
        }

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

    private void ActivationHotkeyInput_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        // The Fluent clear button inside the box runs the internal EditingCommands.Clear, which would only empty the box; make it clear the hotkey, as Backspace does.
        if (e.Command is not RoutedCommand { Name: "Clear" } command || command.OwnerType != typeof(EditingCommands))
            return;

        var source = e.OriginalSource as FrameworkElement;
        if ((source as TextBox ?? source?.TemplatedParent as TextBox) is not { Tag: ActivationHotkeyInputTag } textBox)
            return;

        _viewModel.General.ClearActivationHotkey();
        ShowActivationHotkey(textBox);
        e.Handled = true;
    }

    private void ActivationHotkeyInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (e.OriginalSource is TextBox { Tag: ActivationHotkeyInputTag } textBox)
            RestoreActivationHotkeyBinding(textBox);
    }

    private static void ShowActivationHotkey(TextBox textBox)
    {
        RestoreActivationHotkeyBinding(textBox);
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        textBox.CaretIndex = textBox.Text.Length;
    }

    private static void RestoreActivationHotkeyBinding(TextBox textBox)
    {
        // Text written around the recorder, such as a UI Automation SetValue from dictation, replaces the one-way binding with a local value; put the binding back so the box keeps showing the real hotkey.
        if (BindingOperations.IsDataBound(textBox, TextBox.TextProperty))
            return;

        textBox.SetBinding(TextBox.TextProperty, new Binding($"{nameof(GeneralSettingsViewModel.Settings)}.{nameof(Settings.ActivationHotkey)}") { Mode = BindingMode.OneWay });
    }

    private void OnClosed(object sender, EventArgs e)
    {
        SaveSettings();
    }
}
