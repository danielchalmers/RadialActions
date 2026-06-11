using RadialActions.Properties;

namespace RadialActions.Tests;

public class SettingsSerializationTests
{
    [Fact]
    public void DeserializeFromJson_NormalizesInvalidData()
    {
        const string json = """
        {
          "ActivationHotkey": null,
          "Size": 0,
          "Actions": [
            null,
            {
              "Name": null,
              "Icon": "",
              "Type": 999,
              "IsEnabled": false,
              "Parameter": null,
              "Arguments": null,
              "WorkingDirectory": null
            }
          ]
        }
        """;

        var settings = Settings.DeserializeFromJson(json);

        Assert.Equal(Settings.DefaultActivationHotkey, settings.ActivationHotkey);
        Assert.Equal(Settings.DefaultSize, settings.Size);
        Assert.False(settings.OpenMenuInScreenCenter);
        Assert.Single(settings.Actions);
        Assert.Equal(PieAction.DefaultName, settings.Actions[0].Name);
        Assert.Equal(PieAction.DefaultIcon, settings.Actions[0].Icon);
        Assert.Equal(ActionType.None, settings.Actions[0].Type);
        Assert.False(settings.Actions[0].IsEnabled);
        Assert.Equal(string.Empty, settings.Actions[0].Parameter);
        Assert.Equal(string.Empty, settings.Actions[0].Arguments);
        Assert.Equal(string.Empty, settings.Actions[0].WorkingDirectory);
    }

    [Fact]
    public void DeserializeFromJson_RespectsEmptyActionsList()
    {
        const string json = """
        {
          "ActivationHotkey": "",
          "Size": 320,
          "Actions": []
        }
        """;

        var settings = Settings.DeserializeFromJson(json);

        Assert.Equal(string.Empty, settings.ActivationHotkey);
        Assert.Equal(320, settings.Size);
        Assert.Empty(settings.Actions);
    }

    [Fact]
    public void SerializeToJson_RoundTripsCoreValues()
    {
        var settings = Settings.DeserializeFromJson("{}");
        settings.ActivationHotkey = "Ctrl+Shift+R";
        settings.Size = 512;
        settings.OpenMenuInScreenCenter = true;
        settings.Actions = new System.Collections.ObjectModel.ObservableCollection<PieAction>
        {
            PieAction.CreateKeyAction("Mute"),
            PieAction.CreateShellAction("Explorer", "explorer.exe")
        };
        settings.Actions[1].IsEnabled = false;

        var json = settings.SerializeToJson();
        var loaded = Settings.DeserializeFromJson(json);

        Assert.Equal("Ctrl+Shift+R", loaded.ActivationHotkey);
        Assert.Equal(512, loaded.Size);
        Assert.True(loaded.OpenMenuInScreenCenter);
        Assert.Equal(2, loaded.Actions.Count);
        Assert.Equal(ActionType.Key, loaded.Actions[0].Type);
        Assert.Equal("Mute", loaded.Actions[0].Parameter);
        Assert.True(loaded.Actions[0].IsEnabled);
        Assert.Equal(ActionType.Shell, loaded.Actions[1].Type);
        Assert.Equal("explorer.exe", loaded.Actions[1].Parameter);
        Assert.False(loaded.Actions[1].IsEnabled);
    }

    [Fact]
    public void DeserializeFromJson_MissingIsEnabled_DefaultsToTrue()
    {
        const string json = """
        {
          "Actions": [
            {
              "Name": "Test",
              "Icon": "*",
              "Type": 1,
              "Parameter": "Mute"
            }
          ]
        }
        """;

        var settings = Settings.DeserializeFromJson(json);

        Assert.Single(settings.Actions);
        Assert.True(settings.Actions[0].IsEnabled);
    }

    [Fact]
    public void Load_MalformedPrimary_PreservesCorruptPrimaryBeforeDefaultsCanSave()
    {
        var directory = CreateTempDirectory();
        var settingsPath = Path.Combine(directory, "RadialActions.settings");
        File.WriteAllText(settingsPath, "{");

        var settings = Settings.LoadAndInitializePersistenceState(settingsPath, out var canBeSaved);

        Assert.True(canBeSaved);
        Assert.Equal(Settings.DefaultActivationHotkey, settings.ActivationHotkey);
        Assert.Equal(Settings.DefaultSize, settings.Size);
        Assert.Equal("{", File.ReadAllText(settingsPath));

        var corruptPath = Assert.Single(Directory.GetFiles(directory, "RadialActions.settings.corrupt-*"));
        Assert.Equal("{", File.ReadAllText(corruptPath));
    }

    [Fact]
    public void Load_EmptyExistingFile_IsTreatedAsCorruptionNotFreshInstall()
    {
        var directory = CreateTempDirectory();
        var settingsPath = Path.Combine(directory, "RadialActions.settings");
        File.WriteAllText(settingsPath, string.Empty);

        var settings = Settings.LoadFromFileWithRecovery(settingsPath);

        Assert.Equal(Settings.DefaultActivationHotkey, settings.ActivationHotkey);
        Assert.Equal(Settings.DefaultSize, settings.Size);
        Assert.True(File.Exists(settingsPath));
        Assert.Single(Directory.GetFiles(directory, "RadialActions.settings.corrupt-*"));
    }

    [Fact]
    public void StartupWritableProbe_DoesNotRewriteSettingsFile()
    {
        var directory = CreateTempDirectory();
        var settingsPath = Path.Combine(directory, "RadialActions.settings");
        const string json = """
        {
          "ActivationHotkey": "Ctrl+Alt+P",
          "Size": 480
        }
        """;
        File.WriteAllText(settingsPath, json);
        var lastWriteTime = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(settingsPath, lastWriteTime);

        var settings = Settings.LoadAndInitializePersistenceState(settingsPath, out var canBeSaved);

        Assert.True(canBeSaved);
        Assert.Equal("Ctrl+Alt+P", settings.ActivationHotkey);
        Assert.Equal(json, File.ReadAllText(settingsPath));
        Assert.Equal(lastWriteTime, File.GetLastWriteTimeUtc(settingsPath));
    }

    [Fact]
    public void Load_LockedUnreadablePrimary_DisablesSavingRecoveredDefaults()
    {
        var directory = CreateTempDirectory();
        var settingsPath = Path.Combine(directory, "RadialActions.settings");
        File.WriteAllText(settingsPath, "{");

        using var lockedSettingsFile = new FileStream(settingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var settings = Settings.LoadAndInitializePersistenceState(settingsPath, out var canBeSaved);

        Assert.False(canBeSaved);
        Assert.Equal(Settings.DefaultActivationHotkey, settings.ActivationHotkey);
        Assert.Empty(Directory.GetFiles(directory, "RadialActions.settings.corrupt-*"));
    }

    [Fact]
    public void Save_WritesAtomicallyAndLeavesNoTemporaryFileAfterSuccess()
    {
        var directory = CreateTempDirectory();
        var settingsPath = Path.Combine(directory, "RadialActions.settings");
        var settings = Settings.DeserializeFromJson("{}");
        settings.ActivationHotkey = "Ctrl+Shift+S";
        settings.Size = 640;

        var saved = settings.SaveToFile(settingsPath);

        Assert.True(saved);
        Assert.Contains("Ctrl+Shift+S", File.ReadAllText(settingsPath));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void Deserialize_PropertyLevelErrors_NormalizeWithoutDroppingUnrelatedValidSettings()
    {
        const string json = """
        {
          "ActivationHotkey": "Ctrl+Alt+L",
          "Size": 420,
          "Actions": "not a collection",
          "OpenMenuInScreenCenter": true
        }
        """;

        var settings = Settings.DeserializeFromJson(json);

        Assert.Equal("Ctrl+Alt+L", settings.ActivationHotkey);
        Assert.Equal(420, settings.Size);
        Assert.True(settings.OpenMenuInScreenCenter);
        Assert.Equal(Settings.CreateDefaultActions().Count, settings.Actions.Count);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "RadialActions.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
