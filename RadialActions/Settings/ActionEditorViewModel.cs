using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace RadialActions;

public partial class ActionEditorViewModel : ObservableObject
{
    public const string CustomKeyActionId = "__custom__";

    private static readonly KeyActionDefinition CustomKeyActionOption =
        new(CustomKeyActionId, "Custom Shortcut...", "⌨️", 0);

    private readonly ActionDefaultsService _actionDefaultsService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedAction))]
    [NotifyPropertyChangedFor(nameof(SelectedActionType))]
    [NotifyPropertyChangedFor(nameof(SelectedKeyActionId))]
    private PieAction _selectedAction;

    public ActionEditorViewModel(ActionDefaultsService actionDefaultsService, IEnumerable<PieAction> actions)
    {
        _actionDefaultsService = actionDefaultsService;
        _actionDefaultsService.TrackExistingDefaults(actions);
    }

    public IReadOnlyList<ActionTypeOption> ActionTypes { get; } =
    [
        new(ActionType.Key, "Key", "⌨️"),
        new(ActionType.Shell, "Shell", "🚀"),
        new(ActionType.Macro, "Macro", "🎬"),
    ];

    public IReadOnlyList<MacroStepTypeOption> MacroStepTypes { get; } =
    [
        new(MacroStepType.Shortcut, "Keys", "⌨️"),
        new(MacroStepType.Text, "Text", "🔤"),
        new(MacroStepType.Delay, "Delay", "⏱️"),
    ];

    public IReadOnlyList<KeyActionDefinition> KeyActionOptions { get; } =
        [.. PieAction.KeyActions, CustomKeyActionOption];
    public bool HasSelectedAction => SelectedAction != null;

    public ActionType SelectedActionType
    {
        get => SelectedAction?.Type ?? ActionType.None;
        set
        {
            if (SelectedAction == null || SelectedAction.Type == value)
                return;

            SelectedAction.Type = value;

            if (value == ActionType.None)
            {
                SelectedAction.Parameter = string.Empty;
                SelectedAction.Arguments = string.Empty;
                SelectedAction.WorkingDirectory = string.Empty;
                SelectedAction.MacroSteps?.Clear();
            }
            else if (value == ActionType.Key)
            {
                _actionDefaultsService.EnsureKeyDefaults(SelectedAction);
            }
            else if (value == ActionType.Shell)
            {
                SelectedAction.Parameter = string.Empty;
            }
            else if (value == ActionType.Macro)
            {
                SelectedAction.Parameter = string.Empty;
                SelectedAction.Arguments = string.Empty;
                SelectedAction.WorkingDirectory = string.Empty;
                EnsureMacroSteps(SelectedAction);
            }

            OnPropertyChanged();
        }
    }

    public string SelectedKeyActionId
    {
        get
        {
            if (SelectedAction == null)
                return string.Empty;

            return PieAction.TryGetKeyAction(SelectedAction.Parameter, out _)
                ? SelectedAction.Parameter
                : CustomKeyActionId;
        }
        set
        {
            if (SelectedAction == null)
                return;

            if (value == CustomKeyActionId)
            {
                if (PieAction.TryGetKeyAction(SelectedAction.Parameter, out _))
                {
                    SelectedAction.Parameter = string.Empty;
                }
            }
            else
            {
                if (SelectedAction.Parameter == value)
                    return;

                SelectedAction.Parameter = value ?? string.Empty;
                if (PieAction.TryGetKeyAction(SelectedAction.Parameter, out var definition))
                {
                    _actionDefaultsService.ApplyKeyDefaults(SelectedAction, definition);
                }
            }

            OnPropertyChanged();
        }
    }

    public void Forget(PieAction action)
    {
        _actionDefaultsService.Forget(action);
    }

    /// <summary>
    /// Makes sure a macro action has a steps collection and at least one step to edit.
    /// </summary>
    private static void EnsureMacroSteps(PieAction action)
    {
        action.MacroSteps ??= [];

        if (action.MacroSteps.Count == 0)
        {
            action.MacroSteps.Add(new MacroStep());
        }
    }

    [RelayCommand]
    private void AddMacroStep()
    {
        if (SelectedAction == null)
            return;

        SelectedAction.MacroSteps ??= [];
        SelectedAction.MacroSteps.Add(new MacroStep());
    }

    [RelayCommand]
    private void RemoveMacroStep(MacroStep step)
    {
        if (step == null)
            return;

        SelectedAction?.MacroSteps?.Remove(step);
    }

    [RelayCommand]
    private void DuplicateMacroStep(MacroStep step)
    {
        var steps = SelectedAction?.MacroSteps;
        if (step == null || steps == null)
            return;

        var index = steps.IndexOf(step);
        if (index < 0)
            return;

        steps.Insert(index + 1, step.Clone());
    }

    [RelayCommand]
    private void MoveMacroStepUp(MacroStep step)
    {
        var steps = SelectedAction?.MacroSteps;
        if (step == null || steps == null)
            return;

        var index = steps.IndexOf(step);
        if (index <= 0)
            return;

        steps.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveMacroStepDown(MacroStep step)
    {
        var steps = SelectedAction?.MacroSteps;
        if (step == null || steps == null)
            return;

        var index = steps.IndexOf(step);
        if (index < 0 || index >= steps.Count - 1)
            return;

        steps.Move(index, index + 1);
    }

    [RelayCommand]
    private void BrowseShellTarget()
    {
        if (SelectedAction == null)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Select app, file, or shortcut",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        SelectedAction.Parameter = dialog.FileName;
    }

    [RelayCommand]
    private void BrowseWorkingDirectory()
    {
        if (SelectedAction == null)
            return;

        var dialog = new OpenFolderDialog
        {
            Title = "Select a working directory"
        };

        var suggested = _actionDefaultsService.GetShellDefaults(SelectedAction.Parameter)?.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(SelectedAction.WorkingDirectory))
        {
            dialog.InitialDirectory = SelectedAction.WorkingDirectory;
        }
        else if (!string.IsNullOrWhiteSpace(suggested))
        {
            dialog.InitialDirectory = suggested;
        }

        if (dialog.ShowDialog() != true)
            return;

        SelectedAction.WorkingDirectory = dialog.FolderName;
    }

    partial void OnSelectedActionChanged(PieAction oldValue, PieAction newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= SelectedActionPropertyChanged;
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += SelectedActionPropertyChanged;
            _actionDefaultsService.EnsureKeyDefaults(newValue);

            if (newValue.Type == ActionType.Macro)
            {
                EnsureMacroSteps(newValue);
            }
        }

        OnPropertyChanged(nameof(SelectedActionType));
        OnPropertyChanged(nameof(SelectedKeyActionId));
    }

    private void SelectedActionPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PieAction.Type))
        {
            OnPropertyChanged(nameof(SelectedActionType));
            OnPropertyChanged(nameof(SelectedKeyActionId));
        }

        if (e.PropertyName != nameof(PieAction.Parameter))
            return;

        OnPropertyChanged(nameof(SelectedKeyActionId));

        if (SelectedAction == null)
            return;

        if (SelectedActionType == ActionType.Shell)
        {
            _actionDefaultsService.ApplyShellDefaults(SelectedAction, SelectedAction.Parameter);
        }
        else if (SelectedActionType == ActionType.Key &&
                 PieAction.TryGetKeyAction(SelectedAction.Parameter, out var definition))
        {
            _actionDefaultsService.ApplyKeyDefaults(SelectedAction, definition);
        }
    }
}

public sealed class ActionTypeOption
{
    public ActionTypeOption(ActionType type, string name, string icon)
    {
        Type = type;
        Name = name;
        Icon = icon;
    }

    public ActionType Type { get; }
    public string Name { get; }
    public string Icon { get; }
}

public sealed class MacroStepTypeOption
{
    public MacroStepTypeOption(MacroStepType type, string name, string icon)
    {
        Type = type;
        Name = name;
        Icon = icon;
    }

    public MacroStepType Type { get; }
    public string Name { get; }
    public string Icon { get; }
}
