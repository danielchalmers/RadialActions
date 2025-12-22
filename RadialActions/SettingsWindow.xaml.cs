using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RadialActions.Properties;

namespace RadialActions;

/// <summary>
/// Settings window for configuring the application.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsWindowViewModel(Settings.Default);
        Closed += (s, e) => SaveSettings();
    }

    public void SelectAction(PieAction action)
    {
        if (action == null)
            return;

        if (DataContext is SettingsWindowViewModel viewModel)
        {
            viewModel.SelectAction(action);
        }
    }

    private void SaveSettings()
    {
        if (Settings.CanBeSaved)
        {
            Settings.Default.Save();
            Log.Information("Settings saved");
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ActivationHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.None)
            return;

        if (key is Key.Back or Key.Delete)
        {
            textBox.Clear();
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
            return;
        }

        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var hotkey = BuildHotkeyString(key, Keyboard.Modifiers);
        if (string.IsNullOrWhiteSpace(hotkey))
            return;

        textBox.Text = hotkey;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
    }

    private static string BuildHotkeyString(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>(4);
        if ((modifiers & ModifierKeys.Control) != 0)
            parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0)
            parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0)
            parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0)
            parts.Add("Win");

        var keyName = GetKeyName(key);
        if (string.IsNullOrWhiteSpace(keyName))
            return string.Empty;

        parts.Add(keyName);
        return string.Join("+", parts);
    }

    private static string GetKeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
            return key.ToString();

        if (key is >= Key.D0 and <= Key.D9)
            return ((int)(key - Key.D0)).ToString();

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return key.ToString();

        if (key is >= Key.F1 and <= Key.F24)
            return key.ToString();

        return key switch
        {
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Escape => "Escape",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.PrintScreen => "PrintScreen",
            Key.Scroll => "ScrollLock",
            Key.Pause => "Pause",
            Key.NumLock => "NumLock",
            Key.CapsLock => "CapsLock",
            Key.Add => "Add",
            Key.Subtract => "Subtract",
            Key.Multiply => "Multiply",
            Key.Divide => "Divide",
            Key.Decimal => "Decimal",
            Key.Oem1 or Key.OemSemicolon => "Semicolon",
            Key.OemPlus => "Equals",
            Key.OemComma => "Comma",
            Key.OemMinus => "Minus",
            Key.OemPeriod => "Period",
            Key.OemQuestion => "Slash",
            Key.OemTilde => "Grave",
            Key.OemOpenBrackets => "OpenBracket",
            Key.OemPipe => "Backslash",
            Key.OemCloseBrackets => "CloseBracket",
            Key.OemQuotes => "Quote",
            _ => string.Empty
        };
    }
}

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public partial class SettingsWindowViewModel : ObservableObject
{
    public const string CustomKeyActionId = "__custom__";
    private const string LegacyDefaultIcon = "⭐";
    private const string UpdatesUrl = "https://github.com/danielchalmers/RadialActions/releases";

    private static readonly KeyActionDefinition CustomKeyActionOption =
        new(CustomKeyActionId, "Custom Shortcut...", "⌨️", 0);

    private readonly Dictionary<PieAction, KeyActionDefinition> _autoKeyDefaults = [];
    private readonly Dictionary<PieAction, ShellDefaults> _autoShellDefaults = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedAction))]
    [NotifyPropertyChangedFor(nameof(SelectedActionType))]
    [NotifyPropertyChangedFor(nameof(SelectedKeyActionId))]
    private PieAction _selectedAction;

    [ObservableProperty]
    private int _selectedActionIndex = -1;

    public Settings Settings { get; }

    /// <summary>
    /// Available action types for the dropdown.
    /// </summary>
    public ActionType[] ActionTypes { get; } = [ActionType.None, ActionType.Key, ActionType.Shell];

    /// <summary>
    /// Available key actions for the picker.
    /// </summary>
    public IReadOnlyList<KeyActionDefinition> KeyActionOptions { get; }

    /// <summary>
    /// Whether an action is currently selected.
    /// </summary>
    public bool HasSelectedAction => SelectedAction != null;

    public SettingsWindowViewModel(Settings settings)
    {
        Settings = settings;
        KeyActionOptions = BuildKeyActionOptions();
        TrackExistingDefaults();
        if (Settings.Actions.Count > 0)
        {
            SelectedActionIndex = 0;
            SelectedAction = Settings.Actions[0];
        }
    }

    public void SelectAction(PieAction action)
    {
        if (action == null)
            return;

        var index = Settings.Actions.IndexOf(action);
        if (index < 0)
            return;

        SelectedActionIndex = index;
        SelectedAction = action;
    }

    public ActionType SelectedActionType
    {
        get => SelectedAction?.Type ?? ActionType.None;
        set
        {
            if (SelectedAction == null)
                return;

            if (SelectedAction.Type == value)
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
                EnsureKeyDefaults(SelectedAction);
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
                    ApplyKeyDefaults(SelectedAction, definition);
                }
            }

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Adds a new action to the list.
    /// </summary>
    [RelayCommand]
    public void AddAction()
    {
        var newAction = new PieAction("Blank action") { Type = ActionType.None };
        Settings.Actions.Add(newAction);
        SelectedAction = newAction;
        SelectedActionIndex = Settings.Actions.Count - 1;
        Log.Debug("Added new action");
    }

    /// <summary>
    /// Removes the selected action from the list.
    /// </summary>
    [RelayCommand]
    public void RemoveAction()
    {
        if (SelectedAction == null || Settings.Actions.Count == 0)
            return;

        var index = SelectedActionIndex;
        _autoKeyDefaults.Remove(SelectedAction);
        _autoShellDefaults.Remove(SelectedAction);
        Settings.Actions.Remove(SelectedAction);

        if (Settings.Actions.Count > 0)
        {
            SelectedActionIndex = Math.Min(index, Settings.Actions.Count - 1);
            SelectedAction = Settings.Actions[SelectedActionIndex];
        }
        else
        {
            SelectedAction = null;
            SelectedActionIndex = -1;
        }

        Log.Debug("Removed action");
    }

    /// <summary>
    /// Moves the selected action up in the list.
    /// </summary>
    [RelayCommand]
    public void MoveUp()
    {
        if (SelectedAction == null || SelectedActionIndex <= 0)
            return;

        var index = SelectedActionIndex;
        Settings.Actions.Move(index, index - 1);
        SelectedActionIndex = index - 1;
    }

    /// <summary>
    /// Moves the selected action down in the list.
    /// </summary>
    [RelayCommand]
    public void MoveDown()
    {
        if (SelectedAction == null || SelectedActionIndex >= Settings.Actions.Count - 1)
            return;

        var index = SelectedActionIndex;
        Settings.Actions.Move(index, index + 1);
        SelectedActionIndex = index + 1;
    }

    /// <summary>
    /// Resets actions to the default configuration.
    /// </summary>
    [RelayCommand]
    public void ResetActions()
    {
        var result = MessageBox.Show(
            "This will replace all your actions with the default set. Continue?",
            "Reset Actions",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        Settings.Actions.Clear();
        _autoKeyDefaults.Clear();
        _autoShellDefaults.Clear();
        foreach (var action in Settings.CreateDefaultActions())
        {
            Settings.Actions.Add(action);
        }

        TrackExistingDefaults();

        if (Settings.Actions.Count > 0)
        {
            SelectedActionIndex = 0;
            SelectedAction = Settings.Actions[0];
        }

        Log.Information("Actions reset to defaults");
    }

    [RelayCommand]
    public void BrowseShellTarget()
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
    public void BrowseWorkingDirectory()
    {
        if (SelectedAction == null)
            return;

        var dialog = new OpenFolderDialog
        {
            Title = "Select a working directory"
        };

        var suggested = GetShellDefaults(SelectedAction.Parameter)?.WorkingDirectory;
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

    [RelayCommand]
    public void OpenExeFolder()
    {
        var directory = App.MainFileInfo.DirectoryName;
        if (string.IsNullOrWhiteSpace(directory))
            return;

        Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
    }

    [RelayCommand]
    public void OpenSettingsFile()
    {
        if (Settings.CanBeSaved)
        {
            Settings.Default.Save();
        }

        if (!Settings.Exists)
        {
            MessageBox.Show(
                "Settings file doesn't exist and couldn't be created.",
                "Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        try
        {
            Process.Start("notepad", Settings.FilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Couldn't open notepad");
            MessageBox.Show(
                "Couldn't open settings file.\n\n" +
                "This app may have been reuploaded without permission. If you paid for it, ask for a refund and download it for free from the original source: https://github.com/danielchalmers/RadialActions.\n\n" +
                $"If it still doesn't work, create a new Issue at that link with details on what happened and include this error: \"{ex.Message}\"",
                "Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void CheckForUpdates()
    {
        Process.Start(new ProcessStartInfo(UpdatesUrl) { UseShellExecute = true });
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
            EnsureKeyDefaults(newValue);
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

        if (e.PropertyName == nameof(PieAction.Parameter))
        {
            OnPropertyChanged(nameof(SelectedKeyActionId));

            if (SelectedAction == null)
                return;

            if (SelectedActionType == ActionType.Shell)
            {
                ApplyShellDefaults(SelectedAction, SelectedAction.Parameter);
            }
            else if (SelectedActionType == ActionType.Key &&
                     PieAction.TryGetKeyAction(SelectedAction.Parameter, out var definition))
            {
                ApplyKeyDefaults(SelectedAction, definition);
            }
        }
    }

    private static IReadOnlyList<KeyActionDefinition> BuildKeyActionOptions()
    {
        var options = new List<KeyActionDefinition>(PieAction.KeyActions)
        {
            CustomKeyActionOption
        };

        return options;
    }

    private void TrackExistingDefaults()
    {
        _autoKeyDefaults.Clear();
        _autoShellDefaults.Clear();

        foreach (var action in Settings.Actions)
        {
            if (action.Type == ActionType.Key)
            {
                if (PieAction.TryGetKeyAction(action.Parameter, out var definition))
                {
                    _autoKeyDefaults[action] = definition;
                }
            }
            else if (action.Type == ActionType.Shell)
            {
                var defaults = GetShellDefaults(action.Parameter);
                if (defaults.HasValue)
                {
                    _autoShellDefaults[action] = defaults.Value;
                }
            }
        }
    }

    private void TrackKeyDefaults(PieAction action)
    {
        if (action.Type != ActionType.Key)
            return;

        if (PieAction.TryGetKeyAction(action.Parameter, out var definition))
        {
            _autoKeyDefaults[action] = definition;
        }
    }

    private void EnsureKeyDefaults(PieAction action)
    {
        if (action.Type != ActionType.Key)
            return;

        if (string.IsNullOrWhiteSpace(action.Parameter))
        {
            action.Parameter = PieAction.KeyActions[0].Id;
        }

        if (PieAction.TryGetKeyAction(action.Parameter, out var definition))
        {
            ApplyKeyDefaults(action, definition);
        }
    }

    private void ApplyKeyDefaults(PieAction action, KeyActionDefinition definition)
    {
        _autoKeyDefaults.TryGetValue(action, out var previous);

        if (ShouldApplyDefault(action.Icon, PieAction.DefaultIcon, previous?.Icon ?? string.Empty) ||
            action.Icon == LegacyDefaultIcon)
        {
            action.Icon = definition.Icon;
        }

        _autoKeyDefaults[action] = definition;
    }

    private void ApplyShellDefaults(PieAction action, string target)
    {
        if (action.Type != ActionType.Shell)
            return;

        var defaults = GetShellDefaults(target);
        if (!defaults.HasValue)
            return;

        _autoShellDefaults.TryGetValue(action, out var previous);
        var next = defaults.Value;

        if (ShouldApplyDefault(action.Name, PieAction.DefaultName, previous.Name ?? string.Empty))
        {
            action.Name = next.Name;
        }

        if (ShouldApplyDefault(action.Icon, PieAction.DefaultIcon, previous.Icon ?? string.Empty) ||
            action.Icon == LegacyDefaultIcon)
        {
            action.Icon = next.Icon;
        }

        if (ShouldApplyDefault(action.WorkingDirectory, string.Empty, previous.WorkingDirectory ?? string.Empty) &&
            !string.IsNullOrWhiteSpace(next.WorkingDirectory))
        {
            action.WorkingDirectory = next.WorkingDirectory;
        }

        _autoShellDefaults[action] = next;
    }

    private static ShellDefaults? GetShellDefaults(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return null;

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                return BuildShellDefaultsFromPath(uri.LocalPath);
            }

            var name = string.IsNullOrWhiteSpace(uri.Host) ? uri.AbsoluteUri : uri.Host;
            return new ShellDefaults(name, "🌐", string.Empty);
        }

        return BuildShellDefaultsFromPath(target);
    }

    private static ShellDefaults BuildShellDefaultsFromPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileName(path);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Open";
        }

        var icon = Directory.Exists(path) ? "📂" : "📁";
        var workingDirectory = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? string.Empty;

        return new ShellDefaults(name, icon, workingDirectory);
    }

    private static bool ShouldApplyDefault(string currentValue, string defaultValue, string previousValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue))
            return true;

        if (!string.IsNullOrWhiteSpace(defaultValue) && currentValue == defaultValue)
            return true;

        if (!string.IsNullOrWhiteSpace(previousValue) && currentValue == previousValue)
            return true;

        return false;
    }

    private readonly record struct ShellDefaults(string Name, string Icon, string WorkingDirectory);
}

