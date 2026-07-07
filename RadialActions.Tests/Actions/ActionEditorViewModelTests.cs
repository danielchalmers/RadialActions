namespace RadialActions.Tests;

public class ActionEditorViewModelTests
{
    private static ActionEditorViewModel CreateViewModel(out PieAction action)
    {
        action = new PieAction("Test action");
        var viewModel = new ActionEditorViewModel(new ActionDefaultsService(), [action])
        {
            SelectedAction = action,
        };
        return viewModel;
    }

    [Fact]
    public void SelectedActionType_SwitchToMacro_SeedsOneStepAndClearsShellFields()
    {
        var viewModel = CreateViewModel(out var action);
        action.Parameter = "explorer.exe";
        action.Arguments = "--foo";
        action.WorkingDirectory = "C:\\";

        viewModel.SelectedActionType = ActionType.Macro;

        Assert.Equal(ActionType.Macro, action.Type);
        Assert.Equal(string.Empty, action.Parameter);
        Assert.Equal(string.Empty, action.Arguments);
        Assert.Equal(string.Empty, action.WorkingDirectory);
        Assert.Single(action.MacroSteps);
    }

    [Fact]
    public void SelectedActionType_SwitchToNone_ClearsMacroSteps()
    {
        var viewModel = CreateViewModel(out var action);
        viewModel.SelectedActionType = ActionType.Macro;
        Assert.Single(action.MacroSteps);

        viewModel.SelectedActionType = ActionType.None;

        Assert.Empty(action.MacroSteps);
    }

    [Fact]
    public void SelectingMacroAction_WithNoSteps_SeedsOneStep()
    {
        var action = new PieAction("Macro") { Type = ActionType.Macro };
        var viewModel = new ActionEditorViewModel(new ActionDefaultsService(), [action])
        {
            SelectedAction = action,
        };

        Assert.Single(action.MacroSteps);
    }

    [Fact]
    public void AddMacroStepCommand_AppendsStep()
    {
        var viewModel = CreateViewModel(out var action);
        viewModel.SelectedActionType = ActionType.Macro;

        viewModel.AddMacroStepCommand.Execute(null);

        Assert.Equal(2, action.MacroSteps.Count);
    }

    [Fact]
    public void RemoveMacroStepCommand_RemovesStep()
    {
        var viewModel = CreateViewModel(out var action);
        viewModel.SelectedActionType = ActionType.Macro;
        var step = action.MacroSteps[0];

        viewModel.RemoveMacroStepCommand.Execute(step);

        Assert.Empty(action.MacroSteps);
    }

    [Fact]
    public void DuplicateMacroStepCommand_InsertsCopyAfterOriginal()
    {
        var viewModel = CreateViewModel(out var action);
        viewModel.SelectedActionType = ActionType.Macro;
        var step = action.MacroSteps[0];
        step.Type = MacroStepType.Text;
        step.Value = "hello";

        viewModel.DuplicateMacroStepCommand.Execute(step);

        Assert.Equal(2, action.MacroSteps.Count);
        Assert.NotSame(step, action.MacroSteps[1]);
        Assert.Equal(MacroStepType.Text, action.MacroSteps[1].Type);
        Assert.Equal("hello", action.MacroSteps[1].Value);
    }

    [Fact]
    public void MoveMacroStepCommands_ReorderSteps()
    {
        var viewModel = CreateViewModel(out var action);
        viewModel.SelectedActionType = ActionType.Macro;
        viewModel.AddMacroStepCommand.Execute(null);
        var first = action.MacroSteps[0];
        var second = action.MacroSteps[1];

        viewModel.MoveMacroStepDownCommand.Execute(first);

        Assert.Same(second, action.MacroSteps[0]);
        Assert.Same(first, action.MacroSteps[1]);

        viewModel.MoveMacroStepUpCommand.Execute(first);

        Assert.Same(first, action.MacroSteps[0]);
        Assert.Same(second, action.MacroSteps[1]);
    }

    [Fact]
    public void MoveMacroStepCommands_AtBounds_DoNothing()
    {
        var viewModel = CreateViewModel(out var action);
        viewModel.SelectedActionType = ActionType.Macro;
        var step = action.MacroSteps[0];

        viewModel.MoveMacroStepUpCommand.Execute(step);
        viewModel.MoveMacroStepDownCommand.Execute(step);

        Assert.Same(step, Assert.Single(action.MacroSteps));
    }
}
