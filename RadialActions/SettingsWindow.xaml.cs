using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    private SettingsWindowViewModel ViewModel => (SettingsWindowViewModel)DataContext;

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
}

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public partial class SettingsWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private PieAction _selectedAction;

    [ObservableProperty]
    private int _selectedActionIndex = -1;

    public Settings Settings { get; }

    /// <summary>
    /// Available action types for the dropdown.
    /// </summary>
    public ActionType[] ActionTypes { get; } = (ActionType[])Enum.GetValues(typeof(ActionType));

    /// <summary>
    /// Whether an action is currently selected.
    /// </summary>
    public bool HasSelectedAction => SelectedAction != null;

    public SettingsWindowViewModel(Settings settings)
    {
        Settings = settings;
        if (Settings.Actions.Count > 0)
        {
            SelectedActionIndex = 0;
            SelectedAction = Settings.Actions[0];
        }
    }

    /// <summary>
    /// Adds a new action to the list.
    /// </summary>
    [RelayCommand]
    public void AddAction()
    {
        var newAction = new PieAction("New Action", "⭐") { Type = ActionType.None };
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
        foreach (var action in Settings.CreateDefaultActions())
        {
            Settings.Actions.Add(action);
        }

        if (Settings.Actions.Count > 0)
        {
            SelectedActionIndex = 0;
            SelectedAction = Settings.Actions[0];
        }

        Log.Information("Actions reset to defaults");
    }
}

