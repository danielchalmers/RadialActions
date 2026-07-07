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
    public void SerializeToJson_RoundTripsMacroSteps()
    {
        var settings = Settings.DeserializeFromJson("{}");
        var macro = new PieAction("My Macro") { Type = ActionType.Macro };
        macro.MacroSteps.Add(new MacroStep { Type = MacroStepType.Shortcut, Value = "Ctrl+C" });
        macro.MacroSteps.Add(new MacroStep { Type = MacroStepType.Delay, DelayMilliseconds = 250 });
        macro.MacroSteps.Add(new MacroStep { Type = MacroStepType.Text, Value = "hello" });
        settings.Actions = [macro];

        var json = settings.SerializeToJson();
        var loaded = Settings.DeserializeFromJson(json);

        var loadedMacro = Assert.Single(loaded.Actions);
        Assert.Equal(ActionType.Macro, loadedMacro.Type);
        Assert.Equal(3, loadedMacro.MacroSteps.Count);
        Assert.Equal(MacroStepType.Shortcut, loadedMacro.MacroSteps[0].Type);
        Assert.Equal("Ctrl+C", loadedMacro.MacroSteps[0].Value);
        Assert.Equal(MacroStepType.Delay, loadedMacro.MacroSteps[1].Type);
        Assert.Equal(250, loadedMacro.MacroSteps[1].DelayMilliseconds);
        Assert.Equal(MacroStepType.Text, loadedMacro.MacroSteps[2].Type);
        Assert.Equal("hello", loadedMacro.MacroSteps[2].Value);
    }

    [Fact]
    public void DeserializeFromJson_NormalizesInvalidMacroSteps()
    {
        const string json = """
        {
          "Actions": [
            {
              "Name": "Macro",
              "Type": 3,
              "MacroSteps": [
                null,
                {
                  "Type": 999,
                  "Value": null,
                  "DelayMilliseconds": -1
                }
              ]
            },
            {
              "Name": "Old Action",
              "Type": 1,
              "Parameter": "Mute",
              "MacroSteps": null
            }
          ]
        }
        """;

        var settings = Settings.DeserializeFromJson(json);

        Assert.Equal(2, settings.Actions.Count);

        var macroStep = Assert.Single(settings.Actions[0].MacroSteps);
        Assert.Equal(MacroStepType.Shortcut, macroStep.Type);
        Assert.Equal(string.Empty, macroStep.Value);
        Assert.Equal(MacroStep.DefaultDelayMilliseconds, macroStep.DelayMilliseconds);

        Assert.NotNull(settings.Actions[1].MacroSteps);
        Assert.Empty(settings.Actions[1].MacroSteps);
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
}
