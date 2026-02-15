namespace RadialActions.Tests;

public class PieActionTests
{
    [Fact]
    public void CreateKeyAction_UnknownId_UsesFirstKnownAction()
    {
        var action = PieAction.CreateKeyAction("not-a-real-id");

        Assert.Equal(ActionType.Key, action.Type);
        Assert.Equal(PieAction.KeyActions[0].Id, action.Parameter);
        Assert.Equal(PieAction.KeyActions[0].Name, action.Name);
        Assert.Equal(PieAction.KeyActions[0].Icon, action.Icon);
    }

    [Fact]
    public void CreateShellAction_SetsExpectedFields()
    {
        var action = PieAction.CreateShellAction("Docs", "https://example.com", "*", "--foo", "C:\\");

        Assert.Equal(ActionType.Shell, action.Type);
        Assert.Equal("Docs", action.Name);
        Assert.Equal("*", action.Icon);
        Assert.Equal("https://example.com", action.Parameter);
        Assert.Equal("--foo", action.Arguments);
        Assert.Equal("C:\\", action.WorkingDirectory);
    }

    [Fact]
    public void TryGetKeyAction_KnownId_ReturnsDefinition()
    {
        var ok = PieAction.TryGetKeyAction("PlayPause", out var definition);

        Assert.True(ok);
        Assert.NotNull(definition);
        Assert.Equal("PlayPause", definition.Id);
    }
}
