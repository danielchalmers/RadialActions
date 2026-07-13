using System.IO;
using System.Text;
using System.Windows;

namespace RadialActions;

/// <summary>
/// Turns data dropped onto the actions list (files, folders, apps, or links) into prefilled Open actions.
/// </summary>
public static class ActionDropFactory
{
    // OLE formats browsers use to advertise a dragged link; these are not exposed by DataFormats.
    private const string UniformResourceLocatorW = "UniformResourceLocatorW";
    private const string UniformResourceLocator = "UniformResourceLocator";
    private const string MozillaUrl = "text/x-moz-url";

    /// <summary>
    /// Extracts the ordered, de-duplicated launch targets (file/folder paths or URLs) carried by a drop, or an empty list if there are none.
    /// </summary>
    public static IReadOnlyList<string> ExtractTargets(IDataObject data)
    {
        if (data == null)
            return [];

        var targets = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string candidate)
        {
            var trimmed = candidate?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
            {
                targets.Add(trimmed);
            }
        }

        // Files, folders, and shortcuts dragged from Explorer or the desktop.
        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            foreach (var path in paths)
            {
                Add(path);
            }
        }

        // A link dragged from a browser or editor; only accept it when nothing more specific was dropped.
        if (targets.Count == 0)
        {
            var url = ReadUrl(data);
            if (LooksLikeTarget(url))
            {
                Add(url);
            }
        }

        return targets;
    }

    /// <summary>
    /// Builds an Open action for a single dropped target, prefilling its name, icon, and working directory from <see cref="OpenActionDefaults"/>.
    /// </summary>
    public static PieAction CreateAction(string target)
    {
        var defaults = OpenActionDefaults.FromTarget(target)
            ?? new OpenActionDefaults(PieAction.DefaultName, OpenActionDefaults.FileIcon, string.Empty);

        return PieAction.CreateOpenAction(defaults.Name, target, defaults.Icon, workingDirectory: defaults.WorkingDirectory);
    }

    // Reads the most specific link text the drop advertises, preferring the dedicated URL formats over plain text.
    private static string ReadUrl(IDataObject data)
    {
        if (TryReadText(data, UniformResourceLocatorW, Encoding.Unicode, out var unicodeUrl))
            return unicodeUrl;

        if (TryReadText(data, UniformResourceLocator, Encoding.Latin1, out var ansiUrl))
            return ansiUrl;

        // Firefox advertises "URL\nTitle"; keep only the URL.
        if (TryReadText(data, MozillaUrl, Encoding.Unicode, out var mozUrl))
            return FirstLine(mozUrl);

        if (data.GetDataPresent(DataFormats.UnicodeText) && data.GetData(DataFormats.UnicodeText) is string unicodeText)
            return FirstLine(unicodeText);

        if (data.GetDataPresent(DataFormats.Text) && data.GetData(DataFormats.Text) is string text)
            return FirstLine(text);

        return string.Empty;
    }

    // Reads a custom clipboard format that may arrive as a string or a null-terminated byte stream.
    private static bool TryReadText(IDataObject data, string format, Encoding encoding, out string value)
    {
        value = string.Empty;

        if (!data.GetDataPresent(format))
            return false;

        value = data.GetData(format) switch
        {
            string s => s,
            MemoryStream stream => encoding.GetString(stream.ToArray()),
            _ => string.Empty,
        };

        // OLE string formats are null-terminated; drop the terminator and anything after it.
        var nullIndex = value.IndexOf('\0');
        if (nullIndex >= 0)
        {
            value = value.Substring(0, nullIndex);
        }

        value = value.Trim();
        return value.Length > 0;
    }

    private static string FirstLine(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var newlineIndex = value.IndexOfAny(['\r', '\n']);
        return (newlineIndex >= 0 ? value.Substring(0, newlineIndex) : value).Trim();
    }

    // Guards against arbitrary dragged text creating junk actions: accept file/UNC paths and real launchable links, but not opaque "scheme:text" shapes that ordinary prose ("Re: ...", "TODO: ...") also parses as.
    private static bool LooksLikeTarget(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
            (uri.IsFile || uri.IsUnc || uri.Scheme is "http" or "https" or "ftp" or "ftps" or "mailto"))
        {
            return true;
        }

        return File.Exists(candidate) || Directory.Exists(candidate);
    }
}
