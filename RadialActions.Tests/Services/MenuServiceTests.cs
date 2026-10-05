namespace RadialActions.Tests;

public class MenuServiceTests
{
    [Fact]
    public void HasEnabledActions_WithAnEnabledAction_ReturnsTrue()
    {
        PieAction[] actions = [new PieAction("Hidden") { IsEnabled = false }, new PieAction("Shown")];

        Assert.True(MenuService.HasEnabledActions(actions));
    }

    [Fact]
    public void HasEnabledActions_WhenEveryActionIsHidden_ReturnsFalse()
    {
        PieAction[] actions = [new PieAction("One") { IsEnabled = false }, new PieAction("Two") { IsEnabled = false }];

        Assert.False(MenuService.HasEnabledActions(actions));
    }

    [Fact]
    public void HasEnabledActions_WithNoActions_ReturnsFalse()
    {
        Assert.False(MenuService.HasEnabledActions([]));
        Assert.False(MenuService.HasEnabledActions(null));
    }

    [Fact]
    public void HasEnabledActions_IgnoresNullEntries()
    {
        Assert.False(MenuService.HasEnabledActions([null]));
        Assert.True(MenuService.HasEnabledActions([null, new PieAction("Shown")]));
    }
}
