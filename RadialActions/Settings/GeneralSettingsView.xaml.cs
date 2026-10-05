using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace RadialActions;

public partial class GeneralSettingsView
{
    private bool _isActivationHotkeySuspended;

    public GeneralSettingsView()
    {
        InitializeComponent();
        Unloaded += (_, _) => ResumeActivationHotkey();
    }

    private void ActivationHotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // While the box has focus the current hotkey must reach it instead of opening the menu, so it can be recorded again.
        if (_isActivationHotkeySuspended)
            return;

        _isActivationHotkeySuspended = true;
        (Application.Current.MainWindow as MainWindow)?.SuspendActivationHotkey();
    }

    private void ActivationHotkeyBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // The box shows only what the recorder accepted; characters that arrive without a recorded key press, such as Alt+numpad codes or injected text, never go in.
        e.Handled = true;
    }

    private void ActivationHotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        (DataContext as GeneralSettingsViewModel)?.ClearActivationHotkeyHint();
        ResumeActivationHotkey();
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
