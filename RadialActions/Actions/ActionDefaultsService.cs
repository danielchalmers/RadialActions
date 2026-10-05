namespace RadialActions;

public sealed class ActionDefaultsService
{
    public const string LegacyDefaultIcon = "\u2B50";
    public const string LegacyDefaultName = "New Action";
    public const string LegacyBlankActionName = "Blank action";

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
        if (IsUntouchedName(action))
        {
            action.Name = definition.Name;
        }

        if (IsUntouchedIcon(action))
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

        var next = defaults.Value;

        if (IsUntouchedName(action))
        {
            action.Name = next.Name;
        }

        if (IsUntouchedIcon(action))
        {
            action.Icon = next.Icon;
        }

        var previousWorkingDirectory = GetPreviousOpen(action)?.WorkingDirectory;
        if ((string.IsNullOrWhiteSpace(action.WorkingDirectory) || action.WorkingDirectory == previousWorkingDirectory) &&
            !string.IsNullOrWhiteSpace(next.WorkingDirectory))
        {
            action.WorkingDirectory = next.WorkingDirectory;
        }

        _autoOpenDefaults[action] = next;
    }

    // A name the user never edited: blank, the default (including names older versions used), or the last value filled in from a key or target.
    private bool IsUntouchedName(PieAction action)
    {
        var name = action.Name;
        return string.IsNullOrWhiteSpace(name) ||
            name is PieAction.DefaultName or LegacyDefaultName or LegacyBlankActionName ||
            name == GetPreviousKey(action)?.Name ||
            name == GetPreviousOpen(action)?.Name;
    }

    private bool IsUntouchedIcon(PieAction action)
    {
        var icon = action.Icon;
        return string.IsNullOrWhiteSpace(icon) ||
            icon is PieAction.DefaultIcon or LegacyDefaultIcon ||
            icon == GetPreviousKey(action)?.Icon ||
            icon == GetPreviousOpen(action)?.Icon;
    }

    private KeyActionDefinition GetPreviousKey(PieAction action)
        => _autoKeyDefaults.TryGetValue(action, out var previous) ? previous : null;

    private OpenActionDefaults? GetPreviousOpen(PieAction action)
        => _autoOpenDefaults.TryGetValue(action, out var previous) ? previous : null;
}
