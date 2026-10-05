using System.Globalization;

namespace RadialActions;

/// <summary>
/// Text the pie exposes to UI Automation, kept pure so screen reader wording can be tested without a window.
/// </summary>
internal static class PieAccessibilityText
{
    public const string CloseMenuName = "Close menu";

    // The number keys 1-9 run slices directly, so only the first nine slices have an access key.
    public const int MaxAccessKeys = 9;

    /// <summary>
    /// The slice's name as a screen reader should announce it: the full action name (never the trimmed label), or its type when it has no name.
    /// </summary>
    public static string GetSliceName(PieAction action)
    {
        return ActionDisplayText.GetActionName(action?.Name, action?.Type ?? ActionType.None);
    }

    /// <summary>
    /// The digit that runs the slice at <paramref name="index"/>, or an empty string past the ninth slice.
    /// </summary>
    public static string GetAccessKey(int index)
    {
        return index >= 0 && index < MaxAccessKeys
            ? (index + 1).ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>
    /// Announced when the keyboard selection moves, for example "Mute, 2 of 5".
    /// </summary>
    public static string DescribeSelection(PieAction action, int index, int count)
    {
        return $"{GetSliceName(action)}, {index + 1} of {count}";
    }
}
