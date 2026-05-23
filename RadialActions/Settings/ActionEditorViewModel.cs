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

    public ActionType[] ActionTypes { get; } = [ActionType.None, ActionType.Key, ActionType.Shell];
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
            }
            else if (value == ActionType.Key)
            {
                _actionDefaultsService.EnsureKeyDefaults(SelectedAction);
            }
            else if (value == ActionType.Shell)
            {
                SelectedAction.Parameter = string.Empty;
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
