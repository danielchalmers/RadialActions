namespace RadialActions.Tests;

public sealed class ShellActionDefaultsTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "RadialActions.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetDefaults_EmptyTarget_ReturnsNull()
    {
        Assert.Null(ShellActionDefaults.FromTarget(string.Empty));
    }

    [Fact]
    public void GetDefaults_Url_UsesHostNameAndWebIcon()
    {
        var defaults = ShellActionDefaults.FromTarget("https://docs.github.com/en");

        Assert.Equal(new ShellActionDefaults("docs.github.com", ShellActionDefaults.WebIcon, string.Empty), defaults);
    }

    [Fact]
    public void GetDefaults_FilePath_UsesFileNameIconAndWorkingDirectory()
    {
        Directory.CreateDirectory(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "Example App.exe");
        File.WriteAllText(filePath, "test");

        var defaults = ShellActionDefaults.FromTarget(filePath);

        Assert.Equal(new ShellActionDefaults("Example App", ShellActionDefaults.FileIcon, _tempRoot), defaults);
    }

    [Fact]
    public void GetDefaults_FolderUri_UsesFolderNameIconAndWorkingDirectory()
    {
        var folderPath = Path.Combine(_tempRoot, "Docs");
        Directory.CreateDirectory(folderPath);

        var defaults = ShellActionDefaults.FromTarget(new Uri(folderPath).AbsoluteUri);

        Assert.Equal(new ShellActionDefaults("Docs", ShellActionDefaults.FolderIcon, folderPath), defaults);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
