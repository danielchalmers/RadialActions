using RadialActions.Properties;

namespace RadialActions.Tests;

public class SettingsPersistenceTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;

    public SettingsPersistenceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "RadialActionsTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "Test.settings");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Leftover temp directories are harmless.
        }
    }

    private static Settings CreateSettings(string hotkey)
    {
        var settings = Settings.DeserializeFromJson("{}");
        settings.ActivationHotkey = hotkey;
        return settings;
    }

    private string BackupPath => _filePath + ".bak";

    private string[] CorruptCopies => Directory.GetFiles(_directory, "*.corrupt-*");

    [Fact]
    public void SaveToFile_KeepsPreviousVersionAsBackup()
    {
        Assert.True(CreateSettings("Ctrl+1").SaveToFile(_filePath));
        Assert.True(CreateSettings("Ctrl+2").SaveToFile(_filePath));

        var current = Settings.LoadFromFile(_filePath, out var canBeSaved);
        Assert.True(canBeSaved);
        Assert.Equal("Ctrl+2", current.ActivationHotkey);

        var backup = Settings.DeserializeFromJson(File.ReadAllText(BackupPath));
        Assert.Equal("Ctrl+1", backup.ActivationHotkey);

        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void LoadFromFile_MissingFile_ReturnsDefaults()
    {
        var settings = Settings.LoadFromFile(_filePath, out var canBeSaved);

        Assert.True(canBeSaved);
        Assert.Equal(Settings.DefaultActivationHotkey, settings.ActivationHotkey);
        Assert.Empty(CorruptCopies);
    }

    [Fact]
    public void LoadFromFile_EmptyFile_RestoresFromBackup()
    {
        File.WriteAllText(_filePath, string.Empty);
        CreateSettings("Ctrl+9").SaveToFile(BackupPath);
        File.Delete(BackupPath + ".bak");

        var settings = Settings.LoadFromFile(_filePath, out var canBeSaved);

        Assert.True(canBeSaved);
        Assert.Equal("Ctrl+9", settings.ActivationHotkey);
        Assert.Single(CorruptCopies);
    }

    [Fact]
    public void LoadFromFile_CorruptFile_RestoresFromBackup()
    {
        const string garbage = "!!!not json!!!";
        File.WriteAllText(_filePath, garbage);
        CreateSettings("Ctrl+9").SaveToFile(BackupPath);
        File.Delete(BackupPath + ".bak");

        var settings = Settings.LoadFromFile(_filePath, out var canBeSaved);

        Assert.True(canBeSaved);
        Assert.Equal("Ctrl+9", settings.ActivationHotkey);
        Assert.Equal(garbage, File.ReadAllText(Assert.Single(CorruptCopies)));
        Assert.Equal(garbage, File.ReadAllText(_filePath));
    }

    [Fact]
    public void LoadFromFile_CorruptFile_WithoutBackup_ReturnsDefaults()
    {
        File.WriteAllText(_filePath, "{\"ActivationHotkey\":");

        var settings = Settings.LoadFromFile(_filePath, out var canBeSaved);

        Assert.True(canBeSaved);
        Assert.Equal(Settings.DefaultActivationHotkey, settings.ActivationHotkey);
        Assert.Single(CorruptCopies);
    }

    [Fact]
    public void LoadFromFile_UnreadableFile_DisablesSaving()
    {
        File.WriteAllText(_filePath, "{}");
        using var exclusiveLock = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.None);

        var settings = Settings.LoadFromFile(_filePath, out var canBeSaved);

        Assert.False(canBeSaved);
        Assert.Equal(Settings.DefaultActivationHotkey, settings.ActivationHotkey);
        Assert.Empty(CorruptCopies);
    }
}
