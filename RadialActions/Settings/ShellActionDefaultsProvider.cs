using System.IO;

namespace RadialActions;

public sealed class ShellActionDefaultsProvider
{
    public ShellActionDefaults? GetDefaults(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return null;

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                return BuildDefaultsFromPath(uri.LocalPath);
            }

            var name = string.IsNullOrWhiteSpace(uri.Host) ? uri.AbsoluteUri : uri.Host;
            return new ShellActionDefaults(name, "🌐", string.Empty);
        }

        return BuildDefaultsFromPath(target);
    }

    private static ShellActionDefaults BuildDefaultsFromPath(string path)
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

        var icon = isDirectory ? "📂" : "📁";
        var workingDirectory = isDirectory ? path : Path.GetDirectoryName(path) ?? string.Empty;

        return new ShellActionDefaults(name, icon, workingDirectory);
    }
}
