using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadialActions.Properties;

namespace RadialActions;

public partial class ActionsSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private PieAction _selectedAction;

    public ActionsSettingsViewModel(Settings settings)
    {
        Settings = settings;
        Editor = new ActionEditorViewModel(new ActionDefaultsService(), settings.Actions);

        if (Actions.Count > 0)
        {
            SelectedAction = Actions[0];
        }
    }

    public Settings Settings { get; }
    public ObservableCollection<PieAction> Actions => Settings.Actions;
    public ActionEditorViewModel Editor { get; }

    public void SelectAction(PieAction action)
    {
        if (action == null || !Actions.Contains(action))
            return;

        SelectedAction = action;
    }

    [RelayCommand]
    private void AddAction()
    {
        // The ghost add slice sits at the end of the ring, so the new slice appears where it was clicked.
        var newAction = new PieAction();
        Actions.Add(newAction);
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

        var selectedIndex = SelectedAction == null ? -1 : Actions.IndexOf(SelectedAction);
        var insertionIndex = selectedIndex >= 0 ? selectedIndex + 1 : Actions.Count;

        var added = 0;
        foreach (var target in targets)
        {
            Actions.Insert(insertionIndex, ActionDropFactory.CreateAction(target));
            insertionIndex++;
            added++;
        }

        SelectedAction = Actions[insertionIndex - 1];
        Log.Debug("Added {Count} action(s) from drop", added);
    }

    [RelayCommand]
    private void RemoveAction()
    {
        if (SelectedAction == null)
            return;

        var removed = SelectedAction;
        var index = Actions.IndexOf(removed);
        Editor.Forget(removed);
        Actions.Remove(removed);

        SelectedAction = Actions.Count > 0
            ? Actions[Math.Min(index, Actions.Count - 1)]
            : null;

        Log.Debug("Removed action");
    }

    [RelayCommand]
    private void DuplicateAction()
    {
        if (SelectedAction == null)
            return;

        var source = SelectedAction;
        var copy = source.Clone();
        Actions.Insert(Actions.IndexOf(source) + 1, copy);
        SelectedAction = copy;
        Log.Debug("Duplicated action");
    }

    [RelayCommand]
    private void TestAction()
    {
        if (SelectedAction == null)
            return;

        try
        {
            SelectedAction.Execute();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Test run failed for action: {ActionName}", SelectedAction.Name);
            MessageBox.Show(ex.Message, $"Couldn't run \"{SelectedAction.Name}\"", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    partial void OnSelectedActionChanged(PieAction value)
    {
        Editor.SelectedAction = value;
    }
}
