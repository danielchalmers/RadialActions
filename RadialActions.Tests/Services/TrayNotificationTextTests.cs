namespace RadialActions.Tests;

public class TrayNotificationTextTests
{
    [Fact]
    public void Welcome_WithHotkey_SaysToPressIt()
    {
        var notification = TrayNotificationText.Welcome("Ctrl+Alt+Space", runsAtStartup: true);

        Assert.Equal("Radial Actions is running", notification.Title);
        Assert.Equal("Press Ctrl+Alt+Space to open the menu.", notification.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Welcome_WithoutHotkey_PointsToTheTrayIcon(string hotkey)
    {
        var notification = TrayNotificationText.Welcome(hotkey, runsAtStartup: true);

        Assert.Equal("Radial Actions is running", notification.Title);
        Assert.Equal("Select the Radial Actions tray icon to open the menu.", notification.Message);
    }

    [Theory]
    [InlineData("Ctrl+Alt+Space", "Press Ctrl+Alt+Space to open the menu.")]
    [InlineData(null, "Select the Radial Actions tray icon to open the menu.")]
    public void Welcome_WhenNotRunningAtStartup_SaysHowToTurnItOn(string hotkey, string howToOpen)
    {
        var notification = TrayNotificationText.Welcome(hotkey, runsAtStartup: false);

        Assert.Equal($"{howToOpen} To start Radial Actions when you sign in, select this and turn on Run at Windows startup.", notification.Message);
    }

    [Fact]
    public void HotkeyUnavailable_AppendsHowToFixIt()
    {
        var problem = ActivationHotkeyStatus.GetProblem(HotkeyRegistrationResult.Failed, "Ctrl+Alt+Space");

        var notification = TrayNotificationText.HotkeyUnavailable(problem);

        Assert.Equal("Hotkey unavailable", notification.Title);
        Assert.Equal("Couldn't register Ctrl+Alt+Space. Another app may be using it. Select to change it.", notification.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForStartup_Failed_ShowsTheProblemOnEveryLaunch(bool hasShownWelcome)
    {
        var notification = TrayNotificationText.ForStartup(HotkeyRegistrationResult.Failed, "Ctrl+Alt+Space", hasShownWelcome, runsAtStartup: false, out var isWelcome);

        Assert.False(isWelcome);
        Assert.Equal("Hotkey unavailable", notification.Title);
        Assert.Equal("Couldn't register Ctrl+Alt+Space. Another app may be using it. Select to change it.", notification.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForStartup_Unrecognized_ShowsTheProblemOnEveryLaunch(bool hasShownWelcome)
    {
        var notification = TrayNotificationText.ForStartup(HotkeyRegistrationResult.Unrecognized, "Ctrl+Nope", hasShownWelcome, runsAtStartup: false, out var isWelcome);

        Assert.False(isWelcome);
        Assert.Equal("Hotkey unavailable", notification.Title);
        Assert.Equal("Radial Actions doesn't recognize \"Ctrl+Nope\". Select to change it.", notification.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForStartup_RegisteredFirstLaunch_WelcomesWithTheHotkey(bool runsAtStartup)
    {
        var notification = TrayNotificationText.ForStartup(HotkeyRegistrationResult.Registered, "Ctrl+Alt+Space", hasShownWelcome: false, runsAtStartup, out var isWelcome);

        Assert.True(isWelcome);
        Assert.Equal(TrayNotificationText.Welcome("Ctrl+Alt+Space", runsAtStartup), notification);
    }

    [Fact]
    public void ForStartup_EmptyFirstLaunch_WelcomesWithTheTrayIcon()
    {
        var notification = TrayNotificationText.ForStartup(HotkeyRegistrationResult.Empty, "", hasShownWelcome: false, runsAtStartup: true, out var isWelcome);

        Assert.True(isWelcome);
        Assert.Equal("Radial Actions is running", notification.Title);
        Assert.Equal("Select the Radial Actions tray icon to open the menu.", notification.Message);
    }

    [Theory]
    [InlineData(HotkeyRegistrationResult.Registered)]
    [InlineData(HotkeyRegistrationResult.Empty)]
    public void ForStartup_AfterTheWelcome_ShowsNothing(HotkeyRegistrationResult result)
    {
        var notification = TrayNotificationText.ForStartup(result, "Ctrl+Alt+Space", hasShownWelcome: true, runsAtStartup: false, out var isWelcome);

        Assert.Null(notification);
        Assert.False(isWelcome);
    }

    [Fact]
    public void UpdateAvailable_UsesAThreePartVersion()
    {
        var notification = TrayNotificationText.UpdateAvailable(new Version(1, 4, 0, 0));

        Assert.Equal("Update available", notification.Title);
        Assert.Equal("Version 1.4.0 is ready to download. Select to see what's new.", notification.Message);
    }

    [Theory]
    [InlineData("Ctrl+Alt+Space", "Radial Actions (Ctrl+Alt+Space)")]
    [InlineData(null, "Radial Actions")]
    [InlineData("", "Radial Actions")]
    public void ToolTip_NamesTheHotkeyOnlyWhenItWorks(string registeredHotkey, string expected)
    {
        Assert.Equal(expected, TrayNotificationText.ToolTip(registeredHotkey));
    }
}
