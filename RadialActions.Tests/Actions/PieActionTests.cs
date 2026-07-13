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
    public void CreateOpenAction_SetsExpectedFields()
    {
        var action = PieAction.CreateOpenAction("Docs", "https://example.com", "*", "--foo", "C:\\");

        Assert.Equal(ActionType.Open, action.Type);
        Assert.True(action.IsEnabled);
        Assert.Equal("Docs", action.Name);
        Assert.Equal("*", action.Icon);
        Assert.Equal("https://example.com", action.Parameter);
        Assert.Equal("--foo", action.Arguments);
        Assert.Equal("C:\\", action.WorkingDirectory);
    }

    [Fact]
    public void CreateScriptAction_SetsExpectedFields()
    {
        var action = PieAction.CreateScriptAction("Backup", "Get-Date", "📜", "pwsh.exe", "C:\\Scripts", runHidden: true);

        Assert.Equal(ActionType.Script, action.Type);
        Assert.True(action.IsEnabled);
        Assert.Equal("Backup", action.Name);
        Assert.Equal("📜", action.Icon);
        Assert.Equal("pwsh.exe", action.Parameter);
        Assert.Equal("Get-Date", action.Script);
        Assert.Equal("C:\\Scripts", action.WorkingDirectory);
        Assert.True(action.RunHidden);
    }

    [Fact]
    public void CreateScriptAction_DefaultsRunHiddenToFalse()
    {
        var action = PieAction.CreateScriptAction("Backup", "Get-Date");

        Assert.False(action.RunHidden);
    }

    [Fact]
    public void EncodePowerShellCommand_RoundTripsAsUtf16LeBase64()
    {
        const string script = "Write-Host 'héllo'\r\nGet-Date";

        var encoded = PieAction.EncodePowerShellCommand(script);
        var decoded = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(encoded));

        Assert.Equal(script, decoded);
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
    public void Execute_NoneAction_ThrowsInvalidOperationException()
    {
        var action = new PieAction();

        var ex = Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal("No action configured", ex.Message);
    }

    [Fact]
    public void Execute_OpenActionWithoutTarget_ThrowsInvalidOperationException()
    {
        var action = PieAction.CreateOpenAction("Docs", string.Empty);

        var ex = Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal("Launch target not configured", ex.Message);
    }

    [Fact]
    public void Execute_ScriptActionWithoutScript_ThrowsInvalidOperationException()
    {
        var action = PieAction.CreateScriptAction("Backup", string.Empty);

        var ex = Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal("Script is empty", ex.Message);
    }

    [Fact]
    public void Execute_ScriptTooLong_ThrowsInvalidOperationException()
    {
        var action = PieAction.CreateScriptAction("Big", new string('a', 20000));

        var ex = Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal("Script is too long to run", ex.Message);
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
