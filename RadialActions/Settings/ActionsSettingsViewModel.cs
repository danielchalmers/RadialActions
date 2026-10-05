using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadialActions.Properties;

namespace RadialActions;

public partial class ActionsSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveActionCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    private PieAction _selectedAction;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveActionCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    private int _selectedActionIndex = -1;

    public ActionsSettingsViewModel(Settings settings)
    {
        Settings = settings;
        Editor = new ActionEditorViewModel(new ActionDefaultsService(), settings.Actions);

        // The pie's drag reorder and drops change the list outside these commands; a weak subscription keeps the app-lifetime collection from holding closed windows alive.
        CollectionChangedEventManager.AddHandler(Actions, OnActionsCollectionChanged);

        if (Actions.Count > 0)
        {
            SelectedActionIndex = 0;
            SelectedAction = Actions[0];
        }
    }

    public Settings Settings { get; }
    public ObservableCollection<PieAction> Actions => Settings.Actions;
    public ActionEditorViewModel Editor { get; }

    private int SelectedPosition => SelectedAction == null ? -1 : Actions.IndexOf(SelectedAction);

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
        var newAction = new PieAction { Type = ActionType.None };

        var selectedIndex = SelectedPosition;
        var insertionIndex = selectedIndex >= 0 ? selectedIndex + 1 : Actions.Count;

        Actions.Insert(insertionIndex, newAction);
        SelectedActionIndex = insertionIndex;
        SelectedAction = newAction;
        Log.Debug("Added new action");
    }

    /// <summary>
    /// Creates a prefilled shell action for each dropped target, inserting them after the current selection and selecting the last one.
    /// </summary>
    public void AddDroppedTargets(IReadOnlyList<string> targets)
    {
        if (targets == null || targets.Count == 0)
            return;

        var selectedIndex = SelectedPosition;
        var insertionIndex = selectedIndex >= 0 ? selectedIndex + 1 : Actions.Count;

        var added = 0;
        foreach (var target in targets)
        {
            Actions.Insert(insertionIndex, ActionDropFactory.CreateAction(target));
            insertionIndex++;
            added++;
        }

        SelectedActionIndex = insertionIndex - 1;
        SelectedAction = Actions[SelectedActionIndex];
        Log.Debug("Added {Count} action(s) from drop", added);
    }

    private bool CanRemoveAction() => SelectedPosition >= 0;

    [RelayCommand(CanExecute = nameof(CanRemoveAction))]
    private void RemoveAction()
    {
        var index = SelectedPosition;
        if (index < 0)
            return;

        Editor.Forget(SelectedAction);

        // Select the neighbor before removing so the list commands never read a selection that has left the list, which would briefly disable a focused Remove button.
        var neighborIndex = index < Actions.Count - 1 ? index + 1 : index - 1;
        if (neighborIndex >= 0)
        {
            var neighbor = Actions[neighborIndex];
            SelectedActionIndex = neighborIndex;
            SelectedAction = neighbor;
            Actions.RemoveAt(index);
            SelectedActionIndex = Actions.IndexOf(neighbor);
        }
        else
        {
            SelectedAction = null;
            SelectedActionIndex = -1;
            Actions.RemoveAt(index);
        }

        Log.Debug("Removed action");
    }

    private bool CanMoveUp() => SelectedPosition > 0;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        var index = SelectedPosition;
        if (index <= 0)
            return;

        Actions.Move(index, index - 1);
        SelectedActionIndex = index - 1;
    }

    private bool CanMoveDown()
    {
        var index = SelectedPosition;
        return index >= 0 && index < Actions.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (!CanMoveDown())
            return;

        var index = SelectedPosition;
        Actions.Move(index, index + 1);
        SelectedActionIndex = index + 1;
    }

    partial void OnSelectedActionChanged(PieAction value)
    {
        Editor.SelectedAction = value;
    }

    // Removing or moving items can change which commands apply without changing the selection, for example when the selected action becomes the last one.
    private void OnActionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        RemoveActionCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}
