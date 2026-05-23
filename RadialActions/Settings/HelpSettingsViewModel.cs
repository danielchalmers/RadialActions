using RadialActions.Properties;

namespace RadialActions;

public sealed class HelpSettingsViewModel
{
    public HelpSettingsViewModel(Settings settings)
    {
        Settings = settings;
    }

    public Settings Settings { get; }
}
