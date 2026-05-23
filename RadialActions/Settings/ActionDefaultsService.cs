namespace RadialActions.Settings;

public sealed class ActionDefaultsService
{
    public const string LegacyDefaultIcon = "⭐";

    private readonly ShellActionDefaultsProvider _shellDefaultsProvider;
    private readonly Dictionary<PieAction, KeyActionDefinition> _autoKeyDefaults = [];
    private readonly Dictionary<PieAction, ShellActionDefaults> _autoShellDefaults = [];

    public ActionDefaultsService()
        : this(new ShellActionDefaultsProvider())
    {
    }

    public ActionDefaultsService(ShellActionDefaultsProvider shellDefaultsProvider)
    {
        _shellDefaultsProvider = shellDefaultsProvider;
    }

    public ShellActionDefaults? GetShellDefaults(string target) => _shellDefaultsProvider.GetDefaults(target);

    public void Forget(PieAction action)
    {
        _autoKeyDefaults.Remove(action);
        _autoShellDefaults.Remove(action);
    }

    public void TrackExistingDefaults(IEnumerable<PieAction> actions)
    {
        _autoKeyDefaults.Clear();
        _autoShellDefaults.Clear();

        foreach (var action in actions)
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

    public void EnsureKeyDefaults(PieAction action)
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

    public void ApplyKeyDefaults(PieAction action, KeyActionDefinition definition)
    {
        _autoKeyDefaults.TryGetValue(action, out var previous);

        if (ShouldApplyDefault(action.Icon, PieAction.DefaultIcon, previous?.Icon ?? string.Empty) ||
            action.Icon == LegacyDefaultIcon)
        {
            action.Icon = definition.Icon;
        }

        _autoKeyDefaults[action] = definition;
    }

    public void ApplyShellDefaults(PieAction action, string target)
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
}
