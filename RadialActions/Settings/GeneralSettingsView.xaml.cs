using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace RadialActions;

public partial class GeneralSettingsView
{
    private GeneralSettingsViewModel _viewModel;
    private bool _isActivationHotkeySuspended;

    public GeneralSettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) =>
        {
            _viewModel?.CancelRecordingActivationHotkey();
            ResumeActivationHotkey();
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as GeneralSettingsViewModel;

        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GeneralSettingsViewModel.IsRecordingActivationHotkey))
            return;

        // The current hotkey must reach the button instead of opening the menu while it's being recorded again, and work normally the rest of the time.
        if (_viewModel.IsRecordingActivationHotkey)
            SuspendActivationHotkey();
        else
            ResumeActivationHotkey();
    }

    private void ActivationHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
            return;

        if (_viewModel.IsRecordingActivationHotkey)
            _viewModel.CancelRecordingActivationHotkey();
        else
            _viewModel.StartRecordingActivationHotkey();
    }

    private void ActivationHotkeyButton_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _viewModel?.CancelRecordingActivationHotkey();
    }

    private void ActivationHotkeyButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Until recording starts the button behaves like any other: Enter or Space selects it and Tab moves on.
        if (_viewModel is not { IsRecordingActivationHotkey: true })
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (key == Key.None)
            return;

        // Tab, Esc and Alt+F4 keep their usual meaning and end recording without changing the hotkey.
        if (HotkeyUtil.IsRecorderPassThroughKey(key, modifiers, isActivationHotkey: true))
        {
            _viewModel.CancelRecordingActivationHotkey();
            return;
        }

        // A held key would otherwise record again, or repeat its hint to screen readers, on every auto-repeat.
        if (!e.IsRepeat)
        {
            if ((key is Key.Back or Key.Delete) && modifiers == ModifierKeys.None)
                _viewModel.ClearActivationHotkey();
            else
                _viewModel.RecordActivationHotkey(modifiers, key);
        }

        // Every other key, including Enter, Space and access-key chords, belongs to the recording.
        e.Handled = true;
    }

    private void SuspendActivationHotkey()
    {
        if (_isActivationHotkeySuspended)
            return;

        _isActivationHotkeySuspended = true;
        (Application.Current.MainWindow as MainWindow)?.SuspendActivationHotkey();
    }

    private void ResumeActivationHotkey()
    {
        if (!_isActivationHotkeySuspended)
            return;

        _isActivationHotkeySuspended = false;
        (Application.Current.MainWindow as MainWindow)?.ResumeActivationHotkey();
    }

    private void StatusText_TargetUpdated(object sender, DataTransferEventArgs e)
    {
        // Status lines appear without focus moving to them, so tell screen readers when one gets new text. The line is still collapsed at this point (its visibility binding updates after the text), so the announcement waits for layout.
        if (e.Property != TextBlock.TextProperty || sender is not TextBlock { IsLoaded: true } textBlock || string.IsNullOrEmpty(textBlock.Text))
            return;

        LiveRegionAnnouncer.AnnounceAfterLayout(textBlock);
    }
}
