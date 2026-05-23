using RadialActions.Properties;

namespace RadialActions;

public sealed class GeneralSettingsViewModel
{
    public GeneralSettingsViewModel(Settings settings)
    {
        Settings = settings;
    }

    public Settings Settings { get; }
}
