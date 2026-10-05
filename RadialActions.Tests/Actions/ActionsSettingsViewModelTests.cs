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
        Assert.Equal(1, viewModel.Actions.SelectedActionIndex);
    }

    [Fact]
    public void Constructor_SelectsFirstAction()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var viewModel = CreateViewModel(first, second);

        Assert.Same(first, viewModel.SelectedAction);
        Assert.Equal(0, viewModel.SelectedActionIndex);
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
        Assert.Equal(1, viewModel.SelectedActionIndex);
        Assert.Same(second, viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void AddAction_InsertsNewActionAfterSelectedActionAndSelectsIt()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var third = PieAction.CreateKeyAction("VolumeDown");
        var viewModel = CreateViewModel(first, second, third);
        viewModel.SelectAction(second);

        viewModel.AddActionCommand.Execute(null);

        var added = viewModel.Actions[2];
        Assert.Equal([first, second, added, third], viewModel.Actions);
        Assert.Equal(PieAction.DefaultName, added.Name);
        Assert.Equal("New action", added.Name);
        Assert.Equal(ActionType.None, added.Type);
        Assert.Same(added, viewModel.SelectedAction);
        Assert.Equal(2, viewModel.SelectedActionIndex);
        Assert.Same(added, viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void AddAction_ThenChoosingOpenTarget_NamesActionAfterTarget()
    {
        var viewModel = CreateViewModel(PieAction.CreateKeyAction("Mute"));
        viewModel.AddActionCommand.Execute(null);
        var added = viewModel.SelectedAction;

        viewModel.Editor.SelectedActionType = ActionType.Open;
        added.Parameter = "https://example.com/docs";

        Assert.Equal("example.com", added.Name);
        Assert.Equal(OpenActionDefaults.WebIcon, added.Icon);
    }

    [Fact]
    public void AddAction_ThenChoosingKey_NamesActionAfterKeyAndFollowsLaterKeyChanges()
    {
        var viewModel = CreateViewModel(PieAction.CreateKeyAction("Mute"));
        viewModel.AddActionCommand.Execute(null);
        var added = viewModel.SelectedAction;

        viewModel.Editor.SelectedActionType = ActionType.Key;
        Assert.Equal(PieAction.KeyActions[0].Name, added.Name);

        viewModel.Editor.SelectedKeyActionId = "VolumeUp";
        Assert.Equal("Volume Up", added.Name);

        added.Name = "Louder";
        viewModel.Editor.SelectedKeyActionId = "VolumeDown";
        Assert.Equal("Louder", added.Name);
    }

    [Fact]
    public void ListCommands_EmptyList_CannotExecute()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.AddActionCommand.CanExecute(null));
        Assert.False(viewModel.RemoveActionCommand.CanExecute(null));
        Assert.False(viewModel.MoveUpCommand.CanExecute(null));
        Assert.False(viewModel.MoveDownCommand.CanExecute(null));
    }

    [Fact]
    public void ListCommands_FollowSelectedPosition()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var third = PieAction.CreateKeyAction("VolumeDown");
        var viewModel = CreateViewModel(first, second, third);

        Assert.True(viewModel.RemoveActionCommand.CanExecute(null));
        Assert.False(viewModel.MoveUpCommand.CanExecute(null));
        Assert.True(viewModel.MoveDownCommand.CanExecute(null));

        viewModel.SelectAction(second);
        Assert.True(viewModel.MoveUpCommand.CanExecute(null));
        Assert.True(viewModel.MoveDownCommand.CanExecute(null));

        viewModel.SelectAction(third);
        Assert.True(viewModel.MoveUpCommand.CanExecute(null));
        Assert.False(viewModel.MoveDownCommand.CanExecute(null));
    }

    [Fact]
    public void ListCommands_NoSelection_CannotExecute()
    {
        var viewModel = CreateViewModel(PieAction.CreateKeyAction("Mute"), PieAction.CreateKeyAction("VolumeUp"));

        viewModel.SelectedAction = null;
        viewModel.SelectedActionIndex = -1;

        Assert.False(viewModel.RemoveActionCommand.CanExecute(null));
        Assert.False(viewModel.MoveUpCommand.CanExecute(null));
        Assert.False(viewModel.MoveDownCommand.CanExecute(null));
    }

    [Fact]
    public void ListCommands_RaiseCanExecuteChangedWhenSelectionChanges()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var viewModel = CreateViewModel(first, second);
        var raised = new List<string>();
        viewModel.RemoveActionCommand.CanExecuteChanged += (_, _) => raised.Add("Remove");
        viewModel.MoveUpCommand.CanExecuteChanged += (_, _) => raised.Add("MoveUp");
        viewModel.MoveDownCommand.CanExecuteChanged += (_, _) => raised.Add("MoveDown");

        viewModel.SelectAction(second);

        Assert.Contains("Remove", raised);
        Assert.Contains("MoveUp", raised);
        Assert.Contains("MoveDown", raised);
    }

    [Fact]
    public void ListCommands_RaiseCanExecuteChangedWhenListChangesOutsideCommands()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var viewModel = CreateViewModel(first, second);
        viewModel.SelectAction(second);
        Assert.False(viewModel.MoveDownCommand.CanExecute(null));
        var raised = 0;
        viewModel.MoveDownCommand.CanExecuteChanged += (_, _) => raised++;

        // The pie's drag reorder moves items without going through the commands.
        viewModel.Actions.Move(1, 0);

        Assert.True(raised > 0);
        Assert.True(viewModel.MoveDownCommand.CanExecute(null));
    }

    [Fact]
    public void MoveUp_FirstAction_DoesNothing()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var viewModel = CreateViewModel(first, second);

        viewModel.MoveUpCommand.Execute(null);

        Assert.Equal([first, second], viewModel.Actions);
        Assert.Same(first, viewModel.SelectedAction);
    }

    [Fact]
    public void MoveDown_LastAction_DoesNothing()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var viewModel = CreateViewModel(first, second);
        viewModel.SelectAction(second);

        viewModel.MoveDownCommand.Execute(null);

        Assert.Equal([first, second], viewModel.Actions);
        Assert.Same(second, viewModel.SelectedAction);
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
        Assert.Equal(3, viewModel.SelectedActionIndex);
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
        Assert.Equal(0, viewModel.SelectedActionIndex);
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
        Assert.Equal(0, viewModel.SelectedActionIndex);
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
        Assert.Equal(1, viewModel.SelectedActionIndex);
        Assert.Same(third, viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void RemoveAction_LastAction_SelectsPreviousAction()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var third = PieAction.CreateKeyAction("VolumeDown");
        var viewModel = CreateViewModel(first, second, third);
        viewModel.SelectAction(third);

        viewModel.RemoveActionCommand.Execute(null);

        Assert.Equal([first, second], viewModel.Actions);
        Assert.Same(second, viewModel.SelectedAction);
        Assert.Equal(1, viewModel.SelectedActionIndex);
        Assert.Same(second, viewModel.Editor.SelectedAction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RemoveAction_RemoveStaysExecutableWhileSelectionMoves(int removedIndex)
    {
        // A focused Remove button that disabled itself mid-command would hand keyboard focus to the bare list.
        var viewModel = CreateViewModel(PieAction.CreateKeyAction("Mute"), PieAction.CreateKeyAction("VolumeUp"), PieAction.CreateKeyAction("VolumeDown"));
        viewModel.SelectAction(viewModel.Actions[removedIndex]);
        var canExecuteOnEachChange = new List<bool>();
        viewModel.RemoveActionCommand.CanExecuteChanged += (_, _) => canExecuteOnEachChange.Add(viewModel.RemoveActionCommand.CanExecute(null));

        viewModel.RemoveActionCommand.Execute(null);

        Assert.NotEmpty(canExecuteOnEachChange);
        Assert.DoesNotContain(false, canExecuteOnEachChange);
        Assert.Equal(2, viewModel.Actions.Count);
        Assert.Same(viewModel.Actions[viewModel.SelectedActionIndex], viewModel.SelectedAction);
    }

    [Fact]
    public void RemoveAction_ClearsSelectionWhenListBecomesEmpty()
    {
        var only = PieAction.CreateKeyAction("Mute");
        var viewModel = CreateViewModel(only);

        viewModel.RemoveActionCommand.Execute(null);

        Assert.Empty(viewModel.Actions);
        Assert.Null(viewModel.SelectedAction);
        Assert.Equal(-1, viewModel.SelectedActionIndex);
        Assert.Null(viewModel.Editor.SelectedAction);
    }

    [Fact]
    public void MoveUp_MovesSelectedActionAndKeepsSelection()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var third = PieAction.CreateKeyAction("VolumeDown");
        var viewModel = CreateViewModel(first, second, third);
        viewModel.SelectAction(second);

        viewModel.MoveUpCommand.Execute(null);

        Assert.Equal([second, first, third], viewModel.Actions);
        Assert.Same(second, viewModel.SelectedAction);
        Assert.Equal(0, viewModel.SelectedActionIndex);
    }

    [Fact]
    public void MoveDown_MovesSelectedActionAndKeepsSelection()
    {
        var first = PieAction.CreateKeyAction("Mute");
        var second = PieAction.CreateKeyAction("VolumeUp");
        var third = PieAction.CreateKeyAction("VolumeDown");
        var viewModel = CreateViewModel(first, second, third);
        viewModel.SelectAction(second);

        viewModel.MoveDownCommand.Execute(null);

        Assert.Equal([first, third, second], viewModel.Actions);
        Assert.Same(second, viewModel.SelectedAction);
        Assert.Equal(2, viewModel.SelectedActionIndex);
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
