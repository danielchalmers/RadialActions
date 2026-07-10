namespace RadialActions.Tests;

public class PieActionTests
{
    [Fact]
    public void CreateKeyAction_UnknownId_UsesFirstKnownAction()
    {
        var action = PieAction.CreateKeyAction("not-a-real-id");

        Assert.Equal(ActionType.Key, action.Type);
        Assert.True(action.IsEnabled);
        Assert.Equal(PieAction.KeyActions[0].Id, action.Parameter);
        Assert.Equal(PieAction.KeyActions[0].Name, action.Name);
        Assert.Equal(PieAction.KeyActions[0].Icon, action.Icon);
    }

    [Fact]
    public void CreateShellAction_SetsExpectedFields()
    {
        var action = PieAction.CreateShellAction("Docs", "https://example.com", "*", "--foo", "C:\\");

        Assert.Equal(ActionType.Shell, action.Type);
        Assert.True(action.IsEnabled);
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

    [Fact]
    public void KeyActions_AllHaveCategoryAndVirtualKey()
    {
        foreach (var definition in PieAction.KeyActions)
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Category));
            Assert.NotEqual(0, definition.VirtualKey);
        }
    }

    [Fact]
    public void CreateSystemAction_UnknownId_UsesFirstKnownAction()
    {
        var action = PieAction.CreateSystemAction("not-a-real-id");

        Assert.Equal(ActionType.System, action.Type);
        Assert.True(action.IsEnabled);
        Assert.Equal(PieAction.SystemActions[0].Id, action.Parameter);
        Assert.Equal(PieAction.SystemActions[0].Name, action.Name);
        Assert.Equal(PieAction.SystemActions[0].Icon, action.Icon);
    }

    [Fact]
    public void TryGetSystemAction_KnownId_ReturnsDefinition()
    {
        var ok = PieAction.TryGetSystemAction("LockWorkstation", out var definition);

        Assert.True(ok);
        Assert.NotNull(definition);
        Assert.Equal("LockWorkstation", definition.Id);
    }

    [Fact]
    public void SystemActions_AreConfiguredCorrectly()
    {
        Assert.NotEmpty(PieAction.SystemActions);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in PieAction.SystemActions)
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Id));
            Assert.False(string.IsNullOrWhiteSpace(definition.Name));
            Assert.False(string.IsNullOrWhiteSpace(definition.Icon));
            Assert.NotNull(definition.Execute);
            Assert.True(ids.Add(definition.Id), $"Duplicate system action id: {definition.Id}");
        }
    }

    [Fact]
    public void Execute_NoneAction_ThrowsInvalidOperationException()
    {
        var action = new PieAction();

        var ex = Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal("No action configured", ex.Message);
    }

    [Fact]
    public void Execute_ShellActionWithoutTarget_ThrowsInvalidOperationException()
    {
        var action = PieAction.CreateShellAction("Docs", string.Empty);

        var ex = Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal("Launch target not configured", ex.Message);
    }

    [Fact]
    public void Execute_SystemActionWithoutParameter_ThrowsInvalidOperationException()
    {
        var action = new PieAction("System") { Type = ActionType.System };

        var ex = Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal("System action not configured", ex.Message);
    }

    [Fact]
    public void Execute_SystemActionWithUnknownId_ThrowsInvalidOperationException()
    {
        var action = new PieAction("System")
        {
            Type = ActionType.System,
            Parameter = "not-a-real-id"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal("System action is unknown", ex.Message);
    }

    [Fact]
    public void Execute_KeyActionWithInvalidShortcut_ThrowsInvalidOperationException()
    {
        var action = new PieAction("Shortcut")
        {
            Type = ActionType.Key,
            Parameter = "DefinitelyNotAHotkey"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal("Shortcut is invalid", ex.Message);
    }
}
