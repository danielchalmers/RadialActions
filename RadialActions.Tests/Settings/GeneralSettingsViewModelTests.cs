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
    public void ClearActivationHotkeyHint_KeepsTheHotkey()
    {
        var viewModel = CreateViewModel("Ctrl+Alt+Space");
        viewModel.RecordActivationHotkey(ModifierKeys.None, Key.A);

        viewModel.ClearActivationHotkeyHint();

        Assert.Equal("Ctrl+Alt+Space", viewModel.Settings.ActivationHotkey);
        Assert.False(viewModel.HasActivationHotkeyHint);
    }

    [Fact]
    public void SettingsWindowViewModel_SharesSettingsWithGeneral()
    {
        var settings = Settings.DeserializeFromJson("{}");

        var viewModel = new SettingsWindowViewModel(settings);

        Assert.Same(settings, viewModel.General.Settings);
        Assert.Null(viewModel.General.ActivationHotkeyHint);
    }

    private static GeneralSettingsViewModel CreateViewModel(string hotkey)
    {
        var settings = Settings.DeserializeFromJson("{}");
        settings.ActivationHotkey = hotkey;
        return new GeneralSettingsViewModel(settings);
    }
}
