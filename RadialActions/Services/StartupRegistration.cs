using System.IO;
using System.Security;
using Microsoft.Win32;

namespace RadialActions;

/// <summary>
/// Reads and changes whether a copy of the app starts when the user signs in.
/// </summary>
internal interface IStartupRegistration
{
    bool IsEnabled { get; }

    /// <returns>False when Windows refused the change.</returns>
    bool TrySetEnabled(bool enabled);
}

/// <summary>
/// Run at Windows startup, kept only in the current user's Run key so the toggle always matches what Windows, Task Manager and Settings > Apps > Startup show.
/// </summary>
internal sealed class StartupRegistration : IStartupRegistration
{
    // Earlier versions use the same name, so their entries are recognized.
    private const string ValueName = "RadialActions";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    // Task Manager and Settings > Apps > Startup keep their own on/off override for each Run value here.
    private const string StartupApprovedKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly string _exePath;

    public StartupRegistration(string exePath)
    {
        _exePath = exePath;
    }

    /// <summary>
    /// The registration for the running copy of the app.
    /// </summary>
    public static StartupRegistration ForCurrentApp() => new(App.MainFileInfo.FullName);

    /// <summary>
    /// True when the Run value starts this copy and nobody turned it off in Task Manager.
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            if (!PointsAt(runKey?.GetValue(ValueName) as string, _exePath))
                return false;

            using var startupApprovedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath);
            return !IsDisabledInTaskManager(startupApprovedKey?.GetValue(ValueName) as byte[]);
        }
    }

    public bool TrySetEnabled(bool enabled)
    {
        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                runKey.SetValue(ValueName, FormatCommand(_exePath));
            }
            else if (PointsAt(runKey.GetValue(ValueName) as string, _exePath))
            {
                runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            else
            {
                // The value belongs to another copy of the app, or there isn't one.
                return true;
            }

            // Turning it on has to clear a Task Manager override that would keep it from starting; turning it off doesn't need the override any more.
            using var startupApprovedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath, writable: true);
            startupApprovedKey?.DeleteValue(ValueName, throwOnMissingValue: false);

            Log.Information("Run at Windows startup turned {State} for {Path}", enabled ? "on" : "off", _exePath);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            Log.Error(ex, "Couldn't change Run at Windows startup");
            return false;
        }
    }

    /// <summary>
    /// The Run value for an exe, quoted so Windows can't split a path that has spaces.
    /// </summary>
    internal static string FormatCommand(string exePath) => $"\"{exePath}\"";

    /// <summary>
    /// Whether a Run value starts the given exe, quoted or written without quotes as earlier versions did.
    /// </summary>
    internal static bool PointsAt(string command, string exePath)
    {
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(exePath))
            return false;

        var target = command.Trim();
        if (target.StartsWith('"'))
        {
            var closingQuote = target.IndexOf('"', 1);
            target = closingQuote > 0 ? target[1..closingQuote] : target[1..];
        }

        return string.Equals(target, exePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a StartupApproved value marks the entry as turned off. The first byte is even (02) when it's on and odd (03, followed by when it was turned off) when it's off.
    /// </summary>
    internal static bool IsDisabledInTaskManager(byte[] startupApprovedValue)
    {
        return startupApprovedValue is { Length: > 0 } && (startupApprovedValue[0] & 1) == 1;
    }
}
