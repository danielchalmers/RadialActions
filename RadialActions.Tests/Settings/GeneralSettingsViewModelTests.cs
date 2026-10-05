using System.Windows.Input;
using RadialActions.Properties;

namespace RadialActions.Tests;

public sealed class GeneralSettingsViewModelTests
{
    [Fact]
    public void RecordActivationHotkey_ValidCombination_UpdatesSettingAndClearsHint()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        viewModel.RecordActivationHotkey(ModifierKeys.None, Key.A);

        var recorded = viewModel.RecordActivationHotkey(ModifierKeys.Control | ModifierKeys.Shift, Key.K);

        Assert.True(recorded);
        Assert.Equal("Ctrl+Shift+K", viewModel.Settings.ActivationHotkey);
        Assert.Null(viewModel.ActivationHotkeyHint);
        Assert.False(viewModel.HasActivationHotkeyHint);
    }

    [Theory]
    [InlineData(Key.Tab, ModifierKeys.None)]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.Space, ModifierKeys.Shift)]
    [InlineData(Key.Enter, ModifierKeys.None)]
    public void RecordActivationHotkey_KeyWithoutCtrlAltOrWin_KeepsHotkeyAndShowsHint(Key key, ModifierKeys modifiers)
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");

        var recorded = viewModel.RecordActivationHotkey(modifiers, key);

        Assert.False(recorded);
        Assert.Equal("Ctrl+Alt+Space", viewModel.Settings.ActivationHotkey);
        Assert.Equal(GeneralSettingsViewModel.MissingModifierHint, viewModel.ActivationHotkeyHint);
        Assert.True(viewModel.HasActivationHotkeyHint);
    }

    [Fact]
    public void RecordActivationHotkey_ReservedChord_NamesTheChordInTheHint()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");

        var recorded = viewModel.RecordActivationHotkey(ModifierKeys.Control, Key.Escape);

        Assert.False(recorded);
        Assert.Equal("Ctrl+Alt+Space", viewModel.Settings.ActivationHotkey);
        Assert.Equal("Windows already uses Ctrl+Escape. Try another combination, such as Ctrl+Alt+Space.", viewModel.ActivationHotkeyHint);
    }

    [Theory]
    [InlineData(Key.F13, ModifierKeys.None, "F13")]
    [InlineData(Key.Pause, ModifierKeys.None, "Pause")]
    [InlineData(Key.Scroll, ModifierKeys.Shift, "Shift+ScrollLock")]
    [InlineData(Key.Space, ModifierKeys.Windows, "Win+Space")]
    public void RecordActivationHotkey_KeysNobodyTypesWith_AreAcceptedWithoutCtrlAltOrWin(Key key, ModifierKeys modifiers, string expected)
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");

        var recorded = viewModel.RecordActivationHotkey(modifiers, key);

        Assert.True(recorded);
        Assert.Equal(expected, viewModel.Settings.ActivationHotkey);
    }

    [Theory]
    [InlineData(Key.Apps, ModifierKeys.Control)]
    [InlineData(Key.ImeProcessed, ModifierKeys.None)]
    [InlineData(Key.DeadCharProcessed, ModifierKeys.None)]
    [InlineData(Key.Oem102, ModifierKeys.None)]
    [InlineData(Key.Oem102, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.Cancel, ModifierKeys.Control)]
    public void RecordActivationHotkey_UnnamedKey_KeepsHotkeyAndShowsHint(Key key, ModifierKeys modifiers)
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");

        var recorded = viewModel.RecordActivationHotkey(modifiers, key);

        Assert.False(recorded);
        Assert.Equal("Ctrl+Alt+Space", viewModel.Settings.ActivationHotkey);
        Assert.Equal(GeneralSettingsViewModel.UnsupportedKeyHint, viewModel.ActivationHotkeyHint);
        Assert.True(viewModel.HasActivationHotkeyHint);
    }

    [Theory]
    [InlineData(Key.LeftCtrl, ModifierKeys.Control)]
    [InlineData(Key.RightAlt, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.LWin, ModifierKeys.Windows)]
    [InlineData(Key.None, ModifierKeys.None)]
    public void RecordActivationHotkey_ModifierOnItsOwn_ChangesNothing(Key key, ModifierKeys modifiers)
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        viewModel.RecordActivationHotkey(ModifierKeys.None, Key.A);

        var recorded = viewModel.RecordActivationHotkey(modifiers, key);

        Assert.False(recorded);
        Assert.Equal("Ctrl+Alt+Space", viewModel.Settings.ActivationHotkey);
        Assert.Equal(GeneralSettingsViewModel.MissingModifierHint, viewModel.ActivationHotkeyHint);
    }

    [Fact]
    public void RecordActivationHotkey_RaisesHintChangeNotifications()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.RecordActivationHotkey(ModifierKeys.None, Key.A);

        Assert.Contains(nameof(GeneralSettingsViewModel.ActivationHotkeyHint), changed);
        Assert.Contains(nameof(GeneralSettingsViewModel.HasActivationHotkeyHint), changed);
    }

    [Fact]
    public void RecordActivationHotkey_RepeatedRejection_RaisesTheHintAgain()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        var hintsPerPress = new List<List<string>>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GeneralSettingsViewModel.ActivationHotkeyHint))
                hintsPerPress[^1].Add(viewModel.ActivationHotkeyHint);
        };

        hintsPerPress.Add([]);
        viewModel.RecordActivationHotkey(ModifierKeys.None, Key.A);
        hintsPerPress.Add([]);
        viewModel.RecordActivationHotkey(ModifierKeys.None, Key.B);

        Assert.All(hintsPerPress, hints =>
        {
            Assert.NotEmpty(hints);
            Assert.Equal(GeneralSettingsViewModel.MissingModifierHint, hints[^1]);
        });
    }

    [Fact]
    public void ClearActivationHotkey_EmptiesSettingAndHint()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        viewModel.RecordActivationHotkey(ModifierKeys.None, Key.A);

        viewModel.ClearActivationHotkey();

        Assert.Equal(string.Empty, viewModel.Settings.ActivationHotkey);
        Assert.Null(viewModel.ActivationHotkeyHint);
    }

    [Fact]
    public void ActivationHotkey_IsNotRecordingUntilStarted()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");

        Assert.False(viewModel.IsRecordingActivationHotkey);
        Assert.Equal(GeneralSettingsViewModel.ActivationHotkeyIdleDescription, viewModel.ActivationHotkeyDescription);
        Assert.Equal("Activation hotkey, Ctrl+Alt+Space", viewModel.ActivationHotkeyAccessibleName);
    }

    [Fact]
    public void StartRecordingActivationHotkey_SwitchesTheDescriptionAndName()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.StartRecordingActivationHotkey();

        Assert.True(viewModel.IsRecordingActivationHotkey);
        Assert.Equal(GeneralSettingsViewModel.ActivationHotkeyRecordingDescription, viewModel.ActivationHotkeyDescription);
        Assert.Equal("Activation hotkey, press a key combination", viewModel.ActivationHotkeyAccessibleName);
        Assert.Contains(nameof(GeneralSettingsViewModel.ActivationHotkeyDescription), changed);
        Assert.Contains(nameof(GeneralSettingsViewModel.ActivationHotkeyAccessibleName), changed);
    }

    [Fact]
    public void RecordActivationHotkey_AcceptedCombination_EndsRecording()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        viewModel.StartRecordingActivationHotkey();

        viewModel.RecordActivationHotkey(ModifierKeys.Control | ModifierKeys.Alt, Key.K);

        Assert.False(viewModel.IsRecordingActivationHotkey);
        Assert.Equal("Ctrl+Alt+K", viewModel.Settings.ActivationHotkey);
    }

    [Fact]
    public void RecordActivationHotkey_RejectedCombination_KeepsRecording()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        viewModel.StartRecordingActivationHotkey();

        viewModel.RecordActivationHotkey(ModifierKeys.None, Key.A);

        Assert.True(viewModel.IsRecordingActivationHotkey);
        Assert.True(viewModel.HasActivationHotkeyHint);
        Assert.Equal("Ctrl+Alt+Space", viewModel.Settings.ActivationHotkey);
    }

    [Fact]
    public void CancelRecordingActivationHotkey_KeepsTheHotkeyAndClearsTheHint()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        viewModel.StartRecordingActivationHotkey();
        viewModel.RecordActivationHotkey(ModifierKeys.None, Key.A);

        viewModel.CancelRecordingActivationHotkey();

        Assert.False(viewModel.IsRecordingActivationHotkey);
        Assert.False(viewModel.HasActivationHotkeyHint);
        Assert.Equal("Ctrl+Alt+Space", viewModel.Settings.ActivationHotkey);
    }

    [Fact]
    public void ClearActivationHotkey_EndsRecording()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        viewModel.StartRecordingActivationHotkey();

        viewModel.ClearActivationHotkey();

        Assert.False(viewModel.IsRecordingActivationHotkey);
        Assert.False(viewModel.HasActivationHotkey);
        Assert.Equal("Activation hotkey, not set", viewModel.ActivationHotkeyAccessibleName);
    }

    [Theory]
    [InlineData("Ctrl+Alt+Space", new[] { "Ctrl", "Alt", "Space" })]
    [InlineData(" Ctrl + K ", new[] { "Ctrl", "K" })]
    [InlineData("F9", new[] { "F9" })]
    [InlineData("", new string[0])]
    [InlineData(null, new string[0])]
    public void ActivationHotkeyKeys_SplitsTheHotkeyIntoKeycaps(string hotkey, string[] expected)
    {
        var viewModel = CreateViewModel(hotkey);

        Assert.Equal(expected, viewModel.ActivationHotkeyKeys);
        Assert.Equal(expected.Length > 0, viewModel.HasActivationHotkey);
    }

    [Fact]
    public void ChangingTheHotkeySetting_RefreshesTheKeycapsAndName()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Settings.ActivationHotkey = "Win+Shift+Q";

        Assert.Contains(nameof(GeneralSettingsViewModel.ActivationHotkeyKeys), changed);
        Assert.Contains(nameof(GeneralSettingsViewModel.HasActivationHotkey), changed);
        Assert.Contains(nameof(GeneralSettingsViewModel.ActivationHotkeyAccessibleName), changed);
        Assert.Equal(["Win", "Shift", "Q"], viewModel.ActivationHotkeyKeys);
    }

    [Fact]
    public void SettingsWindowViewModel_SharesSettingsWithGeneral()
    {
        var settings = Settings.DeserializeFromJson("{}");

        var viewModel = new SettingsWindowViewModel(settings);

        Assert.Same(settings, viewModel.General.Settings);
        Assert.Null(viewModel.General.ActivationHotkeyHint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunOnStartup_ReflectsWindows(bool isEnabled)
    {
        var startup = new FakeStartupRegistration { IsEnabled = isEnabled };

        var viewModel = CreateViewModel("Ctrl+Alt+Space", startup);

        Assert.Equal(isEnabled, viewModel.RunOnStartup);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RunOnStartup_Changed_UpdatesWindowsAndRaisesChange(bool before, bool after)
    {
        var startup = new FakeStartupRegistration { IsEnabled = before };
        var viewModel = CreateViewModel("Ctrl+Alt+Space", startup);
        var raised = new List<string>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.RunOnStartup = after;

        Assert.Equal([after], startup.Requests);
        Assert.Equal(after, viewModel.RunOnStartup);
        Assert.Contains(nameof(GeneralSettingsViewModel.RunOnStartup), raised);
    }

    [Fact]
    public void RunOnStartup_WindowsRefuses_StaysOffAndRaisesChangeSoTheToggleSnapsBack()
    {
        var startup = new FakeStartupRegistration { IsEnabled = false, Refuses = true };
        var viewModel = CreateViewModel("Ctrl+Alt+Space", startup);
        var raised = new List<string>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.RunOnStartup = true;

        Assert.False(viewModel.RunOnStartup);
        Assert.Contains(nameof(GeneralSettingsViewModel.RunOnStartup), raised);
    }

    [Fact]
    public void RunOnStartup_SetToCurrentState_DoesNotWrite()
    {
        var startup = new FakeStartupRegistration { IsEnabled = true };
        var viewModel = CreateViewModel("Ctrl+Alt+Space", startup);

        viewModel.RunOnStartup = true;

        Assert.Empty(startup.Requests);
    }

    private static GeneralSettingsViewModel CreateViewModel(string hotkey, FakeStartupRegistration startup = null)
    {
        var settings = Settings.DeserializeFromJson("{}");
        settings.ActivationHotkey = hotkey;
        return new GeneralSettingsViewModel(settings, startup ?? new FakeStartupRegistration());
    }

    private sealed class FakeStartupRegistration : IStartupRegistration
    {
        public bool IsEnabled { get; set; }

        public bool Refuses { get; init; }

        public List<bool> Requests { get; } = [];

        public bool TrySetEnabled(bool enabled)
        {
            Requests.Add(enabled);
            if (!Refuses)
                IsEnabled = enabled;

            return !Refuses;
        }
    }
}
