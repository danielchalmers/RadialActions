using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace RadialActions;

public partial class ActionEditorViewModel : ObservableObject
{
    public const string CustomKeyActionId = "__custom__";
    public const string PowerShell7Interpreter = "pwsh.exe";

    private static readonly KeyActionDefinition CustomKeyActionOption =
        new(CustomKeyActionId, "Custom shortcut", "⌨️", "Custom", 0);

    private readonly ActionDefaultsService _actionDefaultsService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedAction))]
    [NotifyPropertyChangedFor(nameof(SelectedActionType))]
    [NotifyPropertyChangedFor(nameof(SelectedActionTypeDescription))]
    [NotifyPropertyChangedFor(nameof(SelectedKeyActionId))]
    [NotifyPropertyChangedFor(nameof(ScriptInterpreter))]
    private PieAction _selectedAction;

    public ActionEditorViewModel(ActionDefaultsService actionDefaultsService, IEnumerable<PieAction> actions)
    {
        _actionDefaultsService = actionDefaultsService;
        _actionDefaultsService.TrackExistingDefaults(actions);

        var view = new ListCollectionView((System.Collections.IList)KeyActionOptions);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(KeyActionDefinition.Category)));
        KeyActionOptionsView = view;
    }

    public IReadOnlyList<ActionTypeOption> ActionTypes { get; } =
    [
        new(ActionType.Key, "\uE765"),
        new(ActionType.Open, "\uE8A7"),
        new(ActionType.Script, "\uE756"),
    ];

    public IReadOnlyList<KeyActionDefinition> KeyActionOptions { get; } =
        [.. PieAction.KeyActions, CustomKeyActionOption];

    /// <summary>
    /// Key action options grouped by category for display in the editor.
    /// </summary>
    public ICollectionView KeyActionOptionsView { get; }

    /// <summary>
    /// Suggestions for the script interpreter; the box also accepts any other executable or full path.
    /// </summary>
    public IReadOnlyList<string> ScriptInterpreterOptions { get; } = [PieAction.DefaultScriptInterpreter, PowerShell7Interpreter];

    public bool HasSelectedAction => SelectedAction != null;

    /// <summary>
    /// Help text for the Type field, describing only the selected type (or asking for one when none is chosen).
    /// </summary>
    public string SelectedActionTypeDescription => ActionDisplayText.GetTypeDescription(SelectedActionType);

    /// <summary>
    /// The interpreter of a Script action. Other types store unrelated values in <see cref="PieAction.Parameter"/>, so this reads empty and ignores writes for them.
    /// </summary>
    public string ScriptInterpreter
    {
        get => SelectedActionType == ActionType.Script ? SelectedAction.Parameter : string.Empty;
        set
        {
            if (SelectedActionType != ActionType.Script || SelectedAction.Parameter == value)
                return;

            SelectedAction.Parameter = value ?? string.Empty;
        }
    }

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
                SelectedAction.Script = string.Empty;
                SelectedAction.RunHidden = false;
            }
            else if (value == ActionType.Key)
            {
                _actionDefaultsService.EnsureKeyDefaults(SelectedAction);
            }
            else if (value == ActionType.Open)
            {
                SelectedAction.Parameter = string.Empty;
            }
            else if (value == ActionType.Script)
            {
                SelectedAction.Parameter = PieAction.DefaultScriptInterpreter;

                // Default a fresh Script action to hidden, but keep the user's choice when returning to an already-written script.
                if (string.IsNullOrEmpty(SelectedAction.Script))
                {
                    SelectedAction.RunHidden = true;
                }
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
    private void BrowseOpenTarget()
    {
        if (SelectedAction == null)
            return;

        // Keep a picked shortcut as the .lnk itself so it runs with its own arguments and start folder, the same as dropping it on the list.
        var dialog = new OpenFileDialog
        {
            Title = "Choose an app, file, or shortcut",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true,
            DereferenceLinks = false
        };

        var currentFolder = GetExistingParentFolder(SelectedAction.Parameter);
        if (currentFolder != null)
        {
            dialog.InitialDirectory = currentFolder;
        }

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
            Title = "Choose a working directory"
        };

        var suggested = _actionDefaultsService.GetOpenDefaults(SelectedAction.Parameter)?.WorkingDirectory;
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

    // The folder that contains a file path, when that folder exists; null for URLs, commands and blank values.
    internal static string GetExistingParentFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var folder = Path.GetDirectoryName(path);
        return !string.IsNullOrEmpty(folder) && Directory.Exists(folder) ? folder : null;
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
        OnPropertyChanged(nameof(SelectedActionTypeDescription));
        OnPropertyChanged(nameof(SelectedKeyActionId));
        OnPropertyChanged(nameof(ScriptInterpreter));
    }

    private void SelectedActionPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PieAction.Type))
        {
            OnPropertyChanged(nameof(SelectedActionType));
            OnPropertyChanged(nameof(SelectedActionTypeDescription));
            OnPropertyChanged(nameof(SelectedKeyActionId));
            OnPropertyChanged(nameof(ScriptInterpreter));
        }

        if (e.PropertyName != nameof(PieAction.Parameter))
            return;

        OnPropertyChanged(nameof(SelectedKeyActionId));
        OnPropertyChanged(nameof(ScriptInterpreter));

        if (SelectedAction == null)
            return;

        if (SelectedActionType == ActionType.Open)
        {
            _actionDefaultsService.ApplyOpenDefaults(SelectedAction, SelectedAction.Parameter);
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
    public ActionTypeOption(ActionType type, string glyph)
    {
        Type = type;
        Name = ActionDisplayText.GetTypeName(type);
        Glyph = glyph;
    }

    public ActionType Type { get; }
    public string Name { get; }

    /// <summary>
    /// A Segoe Fluent Icons character shown next to the name.
    /// </summary>
    public string Glyph { get; }

    // Combo box items are named from ToString, so screen readers and type-ahead see the display name.
    public override string ToString() => Name;
}
