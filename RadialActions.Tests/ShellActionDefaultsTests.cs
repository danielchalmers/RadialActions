using RadialActions.Settings;

namespace RadialActions.Tests;

public sealed class ShellActionDefaultsTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "RadialActions.Tests", Guid.NewGuid().ToString("N"));
    private readonly ShellActionDefaultsProvider _provider = new();

    [Fact]
    public void GetDefaults_EmptyTarget_ReturnsNull()
    {
        Assert.Null(_provider.GetDefaults(string.Empty));
    }

    [Fact]
    public void GetDefaults_Url_UsesHostNameAndWebIcon()
    {
        var defaults = _provider.GetDefaults("https://docs.github.com/en");

        Assert.Equal(new ShellActionDefaults("docs.github.com", "🌐", string.Empty), defaults);
    }

    [Fact]
    public void GetDefaults_FilePath_UsesFileNameIconAndWorkingDirectory()
    {
        Directory.CreateDirectory(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "Example App.exe");
        File.WriteAllText(filePath, "test");

        var defaults = _provider.GetDefaults(filePath);

        Assert.Equal(new ShellActionDefaults("Example App", "📁", _tempRoot), defaults);
    }

    [Fact]
    public void GetDefaults_FolderUri_UsesFolderNameIconAndWorkingDirectory()
    {
        var folderPath = Path.Combine(_tempRoot, "Docs");
        Directory.CreateDirectory(folderPath);

        var defaults = _provider.GetDefaults(new Uri(folderPath).AbsoluteUri);

        Assert.Equal(new ShellActionDefaults("Docs", "📂", folderPath), defaults);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
