using RadialActions.Properties;

namespace RadialActions;

public sealed class SettingsWindowViewModel
{
    public SettingsWindowViewModel(Settings settings)
    {
        Settings = settings;
        General = new GeneralSettingsViewModel(settings);
        Actions = new ActionsSettingsViewModel(settings);
        About = new AboutSettingsViewModel(settings);
    }

    public Settings Settings { get; }
    public GeneralSettingsViewModel General { get; }
    public ActionsSettingsViewModel Actions { get; }
    public AboutSettingsViewModel About { get; }

    public void SelectAction(PieAction action)
    {
        Settings.SettingsTabIndex = 1;
        Actions.SelectAction(action);
    }
}
