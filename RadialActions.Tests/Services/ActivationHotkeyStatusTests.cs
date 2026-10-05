namespace RadialActions.Tests;

public class ActivationHotkeyStatusTests
{
    [Fact]
    public void Registered_HasNoProblemOrError()
    {
        Assert.Null(ActivationHotkeyStatus.GetProblem(HotkeyRegistrationResult.Registered, "Ctrl+Alt+Space"));
        Assert.Null(ActivationHotkeyStatus.GetError(HotkeyRegistrationResult.Registered, "Ctrl+Alt+Space"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_PointsToTheTrayIcon(string hotkey)
    {
        Assert.Equal("No hotkey is set.", ActivationHotkeyStatus.GetProblem(HotkeyRegistrationResult.Empty, hotkey));
        Assert.Equal(
            "No hotkey is set. Open the menu from the tray icon, or record a hotkey.",
            ActivationHotkeyStatus.GetError(HotkeyRegistrationResult.Empty, hotkey));
    }

    [Fact]
    public void Unrecognized_QuotesTheHotkey()
    {
        Assert.Equal(
            "Radial Actions doesn't recognize \"hello\".",
            ActivationHotkeyStatus.GetProblem(HotkeyRegistrationResult.Unrecognized, " hello "));
        Assert.Equal(
            "Radial Actions doesn't recognize \"hello\". Record a different combination.",
            ActivationHotkeyStatus.GetError(HotkeyRegistrationResult.Unrecognized, "hello"));
    }

    [Fact]
    public void Failed_SaysAnotherAppMayBeUsingIt()
    {
        Assert.Equal(
            "Couldn't register Alt+Space. Another app may be using it.",
            ActivationHotkeyStatus.GetProblem(HotkeyRegistrationResult.Failed, "Alt+Space"));
        Assert.Equal(
            "Couldn't register Alt+Space. Another app may be using it. Record a different combination.",
            ActivationHotkeyStatus.GetError(HotkeyRegistrationResult.Failed, "Alt+Space"));
    }
}
