using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadialActions.Properties;

namespace RadialActions;

public partial class ActionsSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private PieAction _selectedAction;

    [ObservableProperty]
    private int _selectedActionIndex = -1;

    public ActionsSettingsViewModel(Settings settings)
    {
        Settings = settings;
        Editor = new ActionEditorViewModel(new ActionDefaultsService(), settings.Actions);

        if (Actions.Count > 0)
        {
            SelectedActionIndex = 0;
            SelectedAction = Actions[0];
        }
    }

    public Settings Settings { get; }
    public ObservableCollection<PieAction> Actions => Settings.Actions;
    public ActionEditorViewModel Editor { get; }

    public void SelectAction(PieAction action)
    {
        if (action == null)
            return;

        var index = Actions.IndexOf(action);
        if (index < 0)
            return;

        SelectedActionIndex = index;
        SelectedAction = action;
    }

    [RelayCommand]
    private void AddAction()
    {
        var newAction = new PieAction("Blank action") { Type = ActionType.None };

        var selectedIndex = SelectedAction == null ? -1 : Actions.IndexOf(SelectedAction);
        var insertionIndex = selectedIndex >= 0 ? selectedIndex + 1 : Actions.Count;

        Actions.Insert(insertionIndex, newAction);
        SelectedActionIndex = insertionIndex;
        SelectedAction = newAction;
        Log.Debug("Added new action");
    }

    [RelayCommand]
    private void RemoveAction()
    {
        if (SelectedAction == null || Actions.Count == 0)
            return;

        var removed = SelectedAction;
        var index = SelectedActionIndex;
        Editor.Forget(removed);
        Actions.Remove(removed);

        if (Actions.Count > 0)
        {
            SelectedActionIndex = Math.Min(index, Actions.Count - 1);
            SelectedAction = Actions[SelectedActionIndex];
        }
        else
        {
            SelectedAction = null;
            SelectedActionIndex = -1;
        }

        Log.Debug("Removed action");
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedAction == null || SelectedActionIndex <= 0)
            return;

        var index = SelectedActionIndex;
        Actions.Move(index, index - 1);
        SelectedActionIndex = index - 1;
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedAction == null || SelectedActionIndex >= Actions.Count - 1)
            return;

        var index = SelectedActionIndex;
        Actions.Move(index, index + 1);
        SelectedActionIndex = index + 1;
    }

    partial void OnSelectedActionChanged(PieAction value)
    {
        Editor.SelectedAction = value;
    }
}
