namespace RadialActions.Tests;

public sealed class ActionEditorViewModelTests
{
    [Fact]
    public void SelectedAction_WithInvalidCustomShortcut_ShowsValidationUntilShortcutIsFixed()
    {
        var action = new PieAction("Shortcut")
        {
            Type = ActionType.Key,
            Parameter = "DefinitelyNotAHotkey"
        };

        var viewModel = new ActionEditorViewModel(new ActionDefaultsService(), [action])
        {
            SelectedAction = action
        };

        var initialIssue = Assert.Single(viewModel.ValidationIssues);
        Assert.Equal(ActionValidationSeverity.Error, initialIssue.Severity);
        Assert.Equal("Custom shortcut is invalid.", initialIssue.Message);

        action.Parameter = "Ctrl+Shift+R";

        Assert.Empty(viewModel.ValidationIssues);
    }

    [Fact]
    public void SelectedActionType_UpdatesValidationWhenChoosingSupportedType()
    {
        var action = new PieAction("Blank")
        {
            Type = ActionType.None,
            Parameter = string.Empty
        };

        var viewModel = new ActionEditorViewModel(new ActionDefaultsService(), [action])
        {
            SelectedAction = action
        };

        var initialIssue = Assert.Single(viewModel.ValidationIssues);
        Assert.Equal(ActionValidationSeverity.Error, initialIssue.Severity);
        Assert.Equal("Choose an action type.", initialIssue.Message);

        viewModel.SelectedActionType = ActionType.Key;

        Assert.Equal(ActionType.Key, action.Type);
        Assert.False(viewModel.HasValidationIssues);
        Assert.Empty(viewModel.ValidationIssues);
    }
}
