using System.IO;

namespace RadialActions;

public readonly record struct ShellActionDefaults(string Name, string Icon, string WorkingDirectory)
{
    public const string WebIcon = "\U0001F310";
    public const string FolderIcon = "\U0001F4C2";
    public const string FileIcon = "\U0001F4C1";

    public static ShellActionDefaults? FromTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return null;

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                return FromPath(uri.LocalPath);
            }

            var name = string.IsNullOrWhiteSpace(uri.Host) ? uri.AbsoluteUri : uri.Host;
            return new ShellActionDefaults(name, WebIcon, string.Empty);
        }

        return FromPath(target);
    }

    private static ShellActionDefaults FromPath(string path)
    {
        var isDirectory = Directory.Exists(path);
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileName(path);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Open";
        }

        var icon = isDirectory ? FolderIcon : FileIcon;
        var workingDirectory = isDirectory ? path : Path.GetDirectoryName(path) ?? string.Empty;

        return new ShellActionDefaults(name, icon, workingDirectory);
    }
}
