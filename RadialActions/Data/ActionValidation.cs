using System.IO;

namespace RadialActions;

public enum ActionValidationSeverity
{
    Warning = 0,
    Error = 1,
}

public sealed record ActionValidationIssue(ActionValidationSeverity Severity, string Message);

public static class ActionValidator
{
    public static IReadOnlyList<ActionValidationIssue> Validate(PieAction action)
        => Validate(action, Directory.Exists, File.Exists);

    internal static IReadOnlyList<ActionValidationIssue> Validate(
        PieAction action,
        Func<string, bool> directoryExists,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(directoryExists);
        ArgumentNullException.ThrowIfNull(fileExists);

        if (action == null)
            return [];

        List<ActionValidationIssue> issues = [];

        if (!Enum.IsDefined(action.Type))
        {
            issues.Add(new(ActionValidationSeverity.Error, "This action type is not supported."));
            return issues;
        }

        switch (action.Type)
        {
            case ActionType.None:
                issues.Add(new(ActionValidationSeverity.Error, "Choose an action type."));
                break;

            case ActionType.Key:
                ValidateKeyAction(action, issues);
                break;

            case ActionType.Shell:
                ValidateShellAction(action, issues, directoryExists, fileExists);
                break;
        }

        return issues;
    }

    private static void ValidateKeyAction(PieAction action, List<ActionValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(action.Parameter))
        {
            issues.Add(new(ActionValidationSeverity.Error, "Select a key action or enter a custom shortcut."));
            return;
        }

        if (!PieAction.TryGetKeyAction(action.Parameter, out _) &&
            !HotkeyUtil.TryParse(action.Parameter, out _, out _))
        {
            issues.Add(new(ActionValidationSeverity.Error, "Custom shortcut is invalid."));
        }
    }

    private static void ValidateShellAction(
        PieAction action,
        List<ActionValidationIssue> issues,
        Func<string, bool> directoryExists,
        Func<string, bool> fileExists)
    {
        var target = action.Parameter?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(target))
        {
            issues.Add(new(ActionValidationSeverity.Error, "Choose a target to launch."));
        }
        else if (!LooksLikeAbsoluteUri(target) &&
                 !fileExists(target) &&
                 !directoryExists(target))
        {
            issues.Add(new(
                ActionValidationSeverity.Warning,
                "Target does not exist right now. The shell may still resolve it when the action runs."));
        }

        var workingDirectory = action.WorkingDirectory?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(workingDirectory) &&
            !directoryExists(workingDirectory))
        {
            issues.Add(new(ActionValidationSeverity.Error, "Working directory does not exist."));
        }
    }

    private static bool LooksLikeAbsoluteUri(string target)
    {
        return Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
               !uri.IsFile &&
               !string.IsNullOrWhiteSpace(uri.Scheme);
    }
}
