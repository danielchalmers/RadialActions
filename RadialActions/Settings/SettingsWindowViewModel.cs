using RadialActions.Properties;

namespace RadialActions;

public sealed class SettingsWindowViewModel
{
    public SettingsWindowViewModel(Settings settings)
    {
        Settings = settings;
        General = new GeneralSettingsViewModel(settings);
        Actions = new ActionsSettingsViewModel(settings);
        Advanced = new AdvancedSettingsViewModel(settings);
        Help = new HelpSettingsViewModel(settings);
    }

    public Settings Settings { get; }
    public GeneralSettingsViewModel General { get; }
    public ActionsSettingsViewModel Actions { get; }
    public AdvancedSettingsViewModel Advanced { get; }
    public HelpSettingsViewModel Help { get; }

    public void SelectAction(PieAction action)
    {
        Settings.SettingsTabIndex = 1;
        Actions.SelectAction(action);
    }
}
