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
        Assert.Single(settings.Actions);
        Assert.Equal(PieAction.DefaultName, settings.Actions[0].Name);
        Assert.Equal(PieAction.DefaultIcon, settings.Actions[0].Icon);
        Assert.Equal(ActionType.None, settings.Actions[0].Type);
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
        settings.Actions = new System.Collections.ObjectModel.ObservableCollection<PieAction>
        {
            PieAction.CreateKeyAction("Mute"),
            PieAction.CreateShellAction("Explorer", "explorer.exe")
        };

        var json = settings.SerializeToJson();
        var loaded = Settings.DeserializeFromJson(json);

        Assert.Equal("Ctrl+Shift+R", loaded.ActivationHotkey);
        Assert.Equal(512, loaded.Size);
        Assert.Equal(2, loaded.Actions.Count);
        Assert.Equal(ActionType.Key, loaded.Actions[0].Type);
        Assert.Equal("Mute", loaded.Actions[0].Parameter);
        Assert.Equal(ActionType.Shell, loaded.Actions[1].Type);
        Assert.Equal("explorer.exe", loaded.Actions[1].Parameter);
    }
}
