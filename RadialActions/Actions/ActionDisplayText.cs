namespace RadialActions;

/// <summary>
/// Text shown for an action's type and name, shared by Settings and the pie's screen reader names so both always use the same words.
/// </summary>
public static class ActionDisplayText
{
    public const string NotSetUpTypeName = "Not set up";
    public const string NotSetUpTypeDescription = "Choose a type to set up this action.";

    public static string GetTypeName(ActionType type) => type switch
    {
        ActionType.Key => "Key",
        ActionType.Open => "Open",
        ActionType.Script => "Script",
        _ => NotSetUpTypeName,
    };

    public static string GetTypeDescription(ActionType type) => type switch
    {
        ActionType.Key => "Sends a media key, a volume key, or a keyboard shortcut.",
        ActionType.Open => "Opens an app, file, folder, or website.",
        ActionType.Script => "Runs a PowerShell script stored with the action.",
        _ => NotSetUpTypeDescription,
    };

    /// <summary>
    /// The action's full name without surrounding spaces, or its type name when it has no name.
    /// </summary>
    public static string GetActionName(string name, ActionType type)
    {
        return string.IsNullOrWhiteSpace(name) ? GetTypeName(type) : name.Trim();
    }

    /// <summary>
    /// Builds what a screen reader announces for an action in the list, such as "Mute, Key" or "Explorer, Open, hidden".
    /// </summary>
    public static string GetListItemName(string name, ActionType type, bool isEnabled)
    {
        var typeName = GetTypeName(type);
        var text = string.IsNullOrWhiteSpace(name) ? typeName : $"{name}, {typeName}";
        return isEnabled ? text : $"{text}, hidden";
    }

    /// <summary>
    /// Announced after Move up or Move down, such as "Mute moved to position 2 of 5."
    /// </summary>
    public static string GetMovedAnnouncement(PieAction action, int index, int count)
    {
        return $"{GetActionName(action?.Name, action?.Type ?? ActionType.None)} moved to position {index + 1} of {count}.";
    }

    /// <summary>
    /// Announced after Remove, such as "Mute removed."
    /// </summary>
    public static string GetRemovedAnnouncement(PieAction action)
    {
        return $"{GetActionName(action?.Name, action?.Type ?? ActionType.None)} removed.";
    }
}
