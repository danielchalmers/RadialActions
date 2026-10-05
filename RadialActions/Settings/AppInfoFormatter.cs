using System.Runtime.InteropServices;

namespace RadialActions;

/// <summary>
/// Formats version and platform details the way Windows shows them, for example "1.4.0" and "x64".
/// </summary>
public static class AppInfoFormatter
{
    /// <summary>
    /// Formats a version as three parts (major.minor.build), filling a missing build number with zero.
    /// </summary>
    public static string FormatVersion(Version version)
    {
        if (version == null)
            return string.Empty;

        return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }

    /// <summary>
    /// Formats a version string as three parts, or returns it unchanged when it isn't a version number.
    /// </summary>
    public static string FormatVersion(string version)
    {
        return Version.TryParse(version, out var parsed) ? FormatVersion(parsed) : version;
    }

    public static string FormatArchitecture(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "ARM64",
            Architecture.Arm => "ARM",
            _ => architecture.ToString(),
        };
    }
}
