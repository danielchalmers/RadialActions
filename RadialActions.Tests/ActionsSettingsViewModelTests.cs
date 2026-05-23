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
    public void AddAction_AppendsBlankActionAndSelectsIt()
    {
        var viewModel = CreateViewModel(PieAction.CreateKeyAction("Mute"));

        viewModel.AddActionCommand.Execute(null);

        var added = Assert.Single(viewModel.Actions.Skip(1));
        Assert.Equal("Blank action", added.Name);
        Assert.Equal(ActionType.None, added.Type);
        Assert.Same(added, viewModel.SelectedAction);
        Assert.Equal(1, viewModel.SelectedActionIndex);
        Assert.Same(added, viewModel.Editor.SelectedAction);
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
