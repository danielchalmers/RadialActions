using System.ComponentModel;
using System.IO;

namespace RadialActions;

/// <summary>
/// Builds the notification shown when running an action throws: the title names the action and the message gives the reason as a sentence.
/// </summary>
internal static class ActionFailureMessage
{
    // Windows cuts notification titles off after this many characters.
    internal const int MaxTitleLength = 63;

    private const string TitlePrefix = "Couldn't run ";
    private const string FallbackReason = "Something went wrong. Check the action's settings.";

    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorDirectory = 267;
    private const int ErrorNoAssociation = 1155;
    private const int ErrorCancelled = 1223;

    /// <returns>The notification to show, or null when the user cancelled (for example by declining a UAC prompt), which isn't a failure.</returns>
    public static TrayNotification Create(PieAction action, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is Win32Exception { NativeErrorCode: ErrorCancelled })
        {
            return null;
        }

        return new TrayNotification(GetTitle(action.Name), GetReason(action, exception));
    }

    internal static string GetTitle(string actionName)
    {
        var name = actionName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return TitlePrefix + "the action";
        }

        var title = TitlePrefix + name;
        if (title.Length <= MaxTitleLength)
        {
            return title;
        }

        var keep = MaxTitleLength - TitlePrefix.Length - 1;
        if (char.IsHighSurrogate(name[keep - 1]))
        {
            keep--;
        }

        return TitlePrefix + name[..keep].TrimEnd() + "…";
    }

    private static string GetReason(PieAction action, Exception exception) => exception switch
    {
        Win32Exception { NativeErrorCode: ErrorFileNotFound } or FileNotFoundException => GetFileNotFoundReason(action),
        Win32Exception { NativeErrorCode: ErrorPathNotFound } or DirectoryNotFoundException => "Windows can't find part of the path. Check the target and the working directory.",
        Win32Exception { NativeErrorCode: ErrorDirectory } => "The working directory doesn't exist.",
        Win32Exception { NativeErrorCode: ErrorAccessDenied } or UnauthorizedAccessException => "Windows denied access to the target.",
        Win32Exception { NativeErrorCode: ErrorNoAssociation } => "No app is set up to open this type of file.",
        InvalidOperationException => AsSentence(exception.Message),
        NotSupportedException => "This type of action isn't supported.",
        _ => FallbackReason,
    };

    private static string GetFileNotFoundReason(PieAction action)
    {
        if (action.Type == ActionType.Script)
        {
            var interpreter = string.IsNullOrWhiteSpace(action.Parameter) ? PieAction.DefaultScriptInterpreter : action.Parameter.Trim();
            return $"Windows can't find {interpreter}.";
        }

        return "Windows can't find the target. Check that it still exists.";
    }

    private static string AsSentence(string message)
    {
        var text = message?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return FallbackReason;
        }

        return text[^1] is '.' or '?' or '!' ? text : text + ".";
    }
}
