namespace RadialActions.Tests;

public class MainWindowTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ShouldHideMenuOnHotkey_OnlyReturnsTrueWhenWindowIsVisibleAndActive(
        bool isWindowVisible,
        bool isWindowActive,
        bool expected)
    {
        var shouldHide = MainWindow.ShouldHideMenuOnHotkey(isWindowVisible, isWindowActive);

        Assert.Equal(expected, shouldHide);
    }
}
