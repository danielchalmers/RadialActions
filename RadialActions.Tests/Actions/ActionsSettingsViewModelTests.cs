using System.Collections.ObjectModel;
using RadialActions.Properties;

namespace RadialActions.Tests;

public sealed class ActionsSettingsViewModelTests
{
    [Fact]
    public void SettingsWindowViewModel_SelectAction_SelectsActionTabAndForwardsSelection()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var settings = CreateSettings(first, second);
        settings.SettingsTabIndex = 3;
        var viewModel = new SettingsWindowViewModel(settings);

        viewModel.SelectAction(second);

        Assert.Equal(1, settings.SettingsTabIndex);
        Assert.Same(second, viewModel.Actions.SelectedAction);
    }

    [Fact]
    public void Constructor_SelectsFirstAction()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var viewModel = CreateViewModel(first, second);

        Assert.Same(first, viewModel.SelectedAction);
        Assert.Same(first, viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void SelectAction_SelectsMatchingAction()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var viewModel = CreateViewModel(first, second);

        viewModel.SelectAction(second);

        Assert.Same(second, viewModel.SelectedAction);
        Assert.Same(second, viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void AddAction_AppendsNewActionAtEndAndSelectsIt()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var viewModel = CreateViewModel(first, second);
        viewModel.SelectAction(first);

        viewModel.AddActionCommand.Execute(null);

        var added = viewModel.Actions[2];
        Assert.Equal([first, second, added], viewModel.Actions);
        Assert.Equal(PieAction.DefaultName, added.Name);
        Assert.Equal(ActionType.None, added.Type);
        Assert.Same(added, viewModel.SelectedAction);
        Assert.Same(added, viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void DuplicateAction_InsertsCopyAfterSourceAndSelectsIt()
    {
        var first = PieAction.CreateScriptAction("Backup", "Get-Date", icon: "🧹", interpreter: "pwsh.exe", workingDirectory: @"C:\", runHidden: true);
        first.IsEnabled = false;
        first.Arguments = "/select";
        var second = PieAction.CreateKeyAction("Mute");
        var viewModel = CreateViewModel(first, second);
        viewModel.SelectAction(first);

        viewModel.DuplicateActionCommand.Execute(null);

        var copy = viewModel.Actions[1];
        Assert.Equal([first, copy, second], viewModel.Actions);
        Assert.NotSame(first, copy);
        Assert.Equal(first.Name, copy.Name);
        Assert.Equal(first.Icon, copy.Icon);
        Assert.Equal(first.Type, copy.Type);
        Assert.Equal(first.IsEnabled, copy.IsEnabled);
        Assert.Equal(first.Parameter, copy.Parameter);
        Assert.Equal(first.Arguments, copy.Arguments);
        Assert.Equal(first.WorkingDirectory, copy.WorkingDirectory);
        Assert.Equal(first.Script, copy.Script);
        Assert.Equal(first.RunHidden, copy.RunHidden);
        Assert.Same(copy, viewModel.SelectedAction);
        Assert.Same(copy, viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void AddDroppedTargets_InsertsAfterSelectionAndSelectsLast()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var third = PieAction.CreateKeyAction("VolumeDown");
        var viewModel = CreateViewModel(first, second, third);
        viewModel.SelectAction(second);

        viewModel.AddDroppedTargets(["https://a.com", "https://b.com"]);

        var addedA = viewModel.Actions[2];
        var addedB = viewModel.Actions[3];
        Assert.Equal([first, second, addedA, addedB, third], viewModel.Actions);
        Assert.Equal(ActionType.Open, addedA.Type);
        Assert.Equal("https://a.com", addedA.Parameter);
        Assert.Equal("https://b.com", addedB.Parameter);
        Assert.Same(addedB, viewModel.SelectedAction);
        Assert.Same(addedB, viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void AddDroppedTargets_WithoutSelection_AppendsAtEnd()
    {
        var viewModel = CreateViewModel();

        viewModel.AddDroppedTargets(["https://a.com"]);

        var added = Assert.Single(viewModel.Actions);
        Assert.Equal("https://a.com", added.Parameter);
        Assert.Same(added, viewModel.SelectedAction);
    }

    [Fact]
    public void AddDroppedTargets_EmptyOrNull_DoesNothing()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var viewModel = CreateViewModel(first);

        viewModel.AddDroppedTargets([]);
        viewModel.AddDroppedTargets(null);

        Assert.Equal([first], viewModel.Actions);
        Assert.Same(first, viewModel.SelectedAction);
    }

    [Fact]
    public void ActionEditorViewModel_ActionTypes_HidesNoneType()
    {
        var viewModel = new ActionEditorViewModel(new ActionDefaultsService(), []);

        Assert.Equal([ActionType.Key, ActionType.Open, ActionType.Script], viewModel.ActionTypes.Select(option => option.Type));
    }

    [Fact]
    public void SelectedActionType_SwitchingToScript_DefaultsInterpreterAndRunHidden()
    {
        var action = PieAction.CreateOpenAction("Explorer", "explorer.exe");
        var viewModel = new ActionEditorViewModel(new ActionDefaultsService(), [action])
        {
            SelectedAction = action
        };

        viewModel.SelectedActionType = ActionType.Script;

        Assert.Equal(ActionType.Script, action.Type);
        Assert.Equal(PieAction.DefaultScriptInterpreter, action.Parameter);
        Assert.True(action.RunHidden);
    }

    [Fact]
    public void SelectedActionType_ReturningToScript_PreservesRunHiddenChoice()
    {
        var action = PieAction.CreateScriptAction("Backup", "Get-Date", runHidden: true);
        var viewModel = new ActionEditorViewModel(new ActionDefaultsService(), [action])
        {
            SelectedAction = action
        };
        action.RunHidden = false; // user unchecks Run hidden

        viewModel.SelectedActionType = ActionType.Open;
        viewModel.SelectedActionType = ActionType.Script;

        Assert.Equal(ActionType.Script, action.Type);
        Assert.False(action.RunHidden); // choice preserved, not re-defaulted to hidden
    }

    [Fact]
    public void RemoveAction_SelectsNextAvailableAction()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var third = PieAction.CreateKeyAction("VolumeDown");
        var viewModel = CreateViewModel(first, second, third);
        viewModel.SelectAction(second);

        viewModel.RemoveActionCommand.Execute(null);

        Assert.Equal([first, third], viewModel.Actions);
        Assert.Same(third, viewModel.SelectedAction);
        Assert.Same(third, viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void RemoveAction_ClearsSelectionWhenListBecomesEmpty()
    {
        var only = PieAction.CreateKeyAction("Mute");
        var viewModel = CreateViewModel(only);

        viewModel.RemoveActionCommand.Execute(null);

        Assert.Empty(viewModel.Actions);
        Assert.Null(viewModel.SelectedAction);
        Assert.Null(viewModel.Editor.SelectedAction);
    }

    private static ActionsSettingsViewModel CreateViewModel(params PieAction[] actions)
    {
        return new ActionsSettingsViewModel(CreateSettings(actions));
    }

    private static Settings CreateSettings(params PieAction[] actions)
    {
        var settings = Settings.DeserializeFromJson("{}");
        settings.Actions = new ObservableCollection<PieAction>(actions);
        return settings;
    }
}
