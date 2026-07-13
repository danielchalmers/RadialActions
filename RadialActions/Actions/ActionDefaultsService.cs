namespace RadialActions;

public sealed class ActionDefaultsService
{
    public const string LegacyDefaultIcon = "\u2B50";

    private readonly Dictionary<PieAction, KeyActionDefinition> _autoKeyDefaults = [];
    private readonly Dictionary<PieAction, OpenActionDefaults> _autoOpenDefaults = [];

    public OpenActionDefaults? GetOpenDefaults(string target) => OpenActionDefaults.FromTarget(target);

    public void Forget(PieAction action)
    {
        _autoKeyDefaults.Remove(action);
        _autoOpenDefaults.Remove(action);
    }

    public void TrackExistingDefaults(IEnumerable<PieAction> actions)
    {
        _autoKeyDefaults.Clear();
        _autoOpenDefaults.Clear();

        foreach (var action in actions)
        {
            if (action.Type == ActionType.Key)
            {
                if (PieAction.TryGetKeyAction(action.Parameter, out var definition))
                {
                    _autoKeyDefaults[action] = definition;
                }
            }
            else if (action.Type == ActionType.Open)
            {
                var defaults = GetOpenDefaults(action.Parameter);
                if (defaults.HasValue)
                {
                    _autoOpenDefaults[action] = defaults.Value;
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

    public void ApplyOpenDefaults(PieAction action, string target)
    {
        if (action.Type != ActionType.Open)
            return;

        var defaults = GetOpenDefaults(target);
        if (!defaults.HasValue)
            return;

        _autoOpenDefaults.TryGetValue(action, out var previous);
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

        _autoOpenDefaults[action] = next;
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
