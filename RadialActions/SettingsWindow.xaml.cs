using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
    private readonly SettingsWindowViewModel _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        _viewModel = new SettingsWindowViewModel(Settings.Default);
        DataContext = _viewModel;
        Closed += OnClosed;
    }

    public void SelectAction(PieAction action)
    {
        if (action == null)
            return;

        _viewModel.SelectAction(action);
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
        if (e.Uri.Scheme.Equals("radialactions", StringComparison.OrdinalIgnoreCase) &&
            e.Uri.Host.Equals("licenses", StringComparison.OrdinalIgnoreCase))
        {
            OpenLicensesFile();
            e.Handled = true;
            return;
        }

        Log.Information("Opening link from Settings/Help: {Url}", e.Uri.AbsoluteUri);
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private static void OpenLicensesFile()
    {
        try
        {
            Process.Start("notepad", Path.Combine(App.MainFileInfo.DirectoryName, "Licenses.txt"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Couldn't open Licenses.txt");
            MessageBox.Show(
                $"Couldn't open Licenses.txt.\n\n{ex.Message}",
                "Licenses",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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

        var hotkey = HotkeyUtil.BuildHotkeyString(key, Keyboard.Modifiers);
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

    private void OnClosed(object sender, EventArgs e)
    {
        SaveSettings();
    }
}

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public partial class SettingsWindowViewModel : ObservableObject
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

    [ObservableProperty]
    private int _selectedActionIndex = -1;

    public Settings Settings { get; }
    public string FileVersion { get; } = FileVersionInfo.GetVersionInfo(App.MainFileInfo.FullName)?.FileVersion ?? "Unknown";
    public string Architecture { get; } = RuntimeInformation.ProcessArchitecture.ToString();
    public string RuntimeDescription { get; } = RuntimeInformation.FrameworkDescription;
    public string OsDescription { get; } = RuntimeInformation.OSDescription;
    public string ExecutablePath { get; } = App.MainFileInfo.FullName;

    /// <summary>
    /// Available action types for the dropdown.
    /// </summary>
    public ActionType[] ActionTypes { get; } = [ActionType.None, ActionType.Key, ActionType.Shell];

    /// <summary>
    /// Available key actions for the picker.
    /// </summary>
    public IReadOnlyList<KeyActionDefinition> KeyActionOptions { get; } =
        [.. PieAction.KeyActions, CustomKeyActionOption];

    /// <summary>
    /// Whether an action is currently selected.
    /// </summary>
    public bool HasSelectedAction => SelectedAction != null;

    public SettingsWindowViewModel(Settings settings)
    {
        Settings = settings;
        _actionDefaultsService = new ActionDefaultsService();
        _actionDefaultsService.TrackExistingDefaults(Settings.Actions);
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
        _actionDefaultsService.Forget(SelectedAction);
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

        if (e.PropertyName == nameof(PieAction.Parameter))
        {
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
}
