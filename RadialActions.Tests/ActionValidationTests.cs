namespace RadialActions.Tests;

public sealed class ActionValidationTests
{
    [Fact]
    public void Validate_NoneAction_ReturnsTypeError()
    {
        var action = new PieAction("Blank") { Type = ActionType.None };

        var issues = ActionValidator.Validate(action);

        var issue = Assert.Single(issues);
        Assert.Equal(ActionValidationSeverity.Error, issue.Severity);
        Assert.Equal("Choose an action type.", issue.Message);
    }

    [Fact]
    public void Validate_UnsupportedActionType_ReturnsUnsupportedError()
    {
        var action = new PieAction("Broken") { Type = (ActionType)999 };

        var issues = ActionValidator.Validate(action, _ => false, _ => false);

        var issue = Assert.Single(issues);
        Assert.Equal(ActionValidationSeverity.Error, issue.Severity);
        Assert.Equal("This action type is not supported.", issue.Message);
    }

    [Fact]
    public void Validate_KeyActionWithoutShortcut_ReturnsShortcutError()
    {
        var action = new PieAction("Shortcut") { Type = ActionType.Key, Parameter = "" };

        var issues = ActionValidator.Validate(action);

        var issue = Assert.Single(issues);
        Assert.Equal(ActionValidationSeverity.Error, issue.Severity);
        Assert.Equal("Select a key action or enter a custom shortcut.", issue.Message);
    }

    [Fact]
    public void Validate_KeyActionWithInvalidCustomShortcut_ReturnsShortcutError()
    {
        var action = new PieAction("Shortcut") { Type = ActionType.Key, Parameter = "DefinitelyNotAHotkey" };

        var issues = ActionValidator.Validate(action);

        var issue = Assert.Single(issues);
        Assert.Equal(ActionValidationSeverity.Error, issue.Severity);
        Assert.Equal("Custom shortcut is invalid.", issue.Message);
    }

    [Fact]
    public void Validate_KeyActionWithKnownShortcut_ReturnsNoIssues()
    {
        var action = PieAction.CreateKeyAction("Mute");

        var issues = ActionValidator.Validate(action);

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_ShellActionWithoutTarget_ReturnsTargetError()
    {
        var action = PieAction.CreateShellAction("Docs", string.Empty);

        var issues = ActionValidator.Validate(action, _ => false, _ => false);

        var issue = Assert.Single(issues);
        Assert.Equal(ActionValidationSeverity.Error, issue.Severity);
        Assert.Equal("Choose a target to launch.", issue.Message);
    }

    [Fact]
    public void Validate_ShellActionWithMissingWorkingDirectory_ReturnsError()
    {
        var action = PieAction.CreateShellAction("Docs", "explorer.exe", workingDirectory: @"C:\missing");

        var issues = ActionValidator.Validate(
            action,
            directory => directory == @"C:\exists",
            _ => false);

        Assert.Collection(
            issues,
            issue =>
            {
                Assert.Equal(ActionValidationSeverity.Warning, issue.Severity);
                Assert.Equal("Target does not exist right now. The shell may still resolve it when the action runs.", issue.Message);
            },
            issue =>
            {
                Assert.Equal(ActionValidationSeverity.Error, issue.Severity);
                Assert.Equal("Working directory does not exist.", issue.Message);
            });
    }

    [Fact]
    public void Validate_ShellActionWithMissingTarget_ReturnsWarning()
    {
        var action = PieAction.CreateShellAction("Docs", "explorer.exe");

        var issues = ActionValidator.Validate(action, _ => false, _ => false);

        var issue = Assert.Single(issues);
        Assert.Equal(ActionValidationSeverity.Warning, issue.Severity);
        Assert.Equal("Target does not exist right now. The shell may still resolve it when the action runs.", issue.Message);
    }

    [Fact]
    public void Validate_ShellActionWithAbsoluteUrl_ReturnsNoTargetWarning()
    {
        var action = PieAction.CreateShellAction("Docs", "https://example.com");

        var issues = ActionValidator.Validate(action, _ => false, _ => false);

        Assert.Empty(issues);
    }
}
