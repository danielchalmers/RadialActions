namespace RadialActions.Tests;

public sealed class ActionDisplayTextTests
{
    [Theory]
    [InlineData(ActionType.Key, "Key")]
    [InlineData(ActionType.Open, "Open")]
    [InlineData(ActionType.Script, "Script")]
    [InlineData(ActionType.None, "Not set up")]
    public void GetTypeName_ReturnsDisplayName(ActionType type, string expected)
    {
        Assert.Equal(expected, ActionDisplayText.GetTypeName(type));
    }

    [Fact]
    public void GetTypeName_UnknownValue_TreatsAsNotSetUp()
    {
        Assert.Equal(ActionDisplayText.NotSetUpTypeName, ActionDisplayText.GetTypeName((ActionType)999));
    }

    [Theory]
    [InlineData(ActionType.Key, "Sends a media key, a volume key, or a keyboard shortcut.")]
    [InlineData(ActionType.Open, "Opens an app, file, folder, or website.")]
    [InlineData(ActionType.Script, "Runs a PowerShell script stored with the action.")]
    [InlineData(ActionType.None, "Choose a type to set up this action.")]
    public void GetTypeDescription_DescribesOnlyThatType(ActionType type, string expected)
    {
        Assert.Equal(expected, ActionDisplayText.GetTypeDescription(type));
    }

    [Theory]
    [InlineData("Mute", ActionType.Key, "Mute")]
    [InlineData("  Remote Desktop  ", ActionType.Open, "Remote Desktop")]
    [InlineData("", ActionType.Script, "Script")]
    [InlineData("   ", ActionType.Open, "Open")]
    [InlineData(null, ActionType.None, "Not set up")]
    public void GetActionName_UsesTheTrimmedNameOrFallsBackToTheType(string name, ActionType type, string expected)
    {
        Assert.Equal(expected, ActionDisplayText.GetActionName(name, type));
    }

    [Theory]
    [InlineData("Mute", ActionType.Key, true, "Mute, Key")]
    [InlineData("Explorer", ActionType.Open, false, "Explorer, Open, hidden")]
    [InlineData("New action", ActionType.None, true, "New action, Not set up")]
    [InlineData("", ActionType.Script, true, "Script")]
    [InlineData(null, ActionType.Script, false, "Script, hidden")]
    public void GetListItemName_CombinesNameTypeAndHiddenState(string name, ActionType type, bool isEnabled, string expected)
    {
        Assert.Equal(expected, ActionDisplayText.GetListItemName(name, type, isEnabled));
    }

    [Theory]
    [InlineData(0, 5, "Mute moved to position 1 of 5.")]
    [InlineData(1, 5, "Mute moved to position 2 of 5.")]
    [InlineData(4, 5, "Mute moved to position 5 of 5.")]
    public void GetMovedAnnouncement_NamesTheActionAndItsNewPosition(int index, int count, string expected)
    {
        var action = PieAction.CreateKeyAction("Mute");

        Assert.Equal(expected, ActionDisplayText.GetMovedAnnouncement(action, index, count));
    }

    [Fact]
    public void GetMovedAnnouncement_WithoutAName_UsesTheType()
    {
        var action = new PieAction { Name = "", Type = ActionType.Script };

        Assert.Equal("Script moved to position 3 of 4.", ActionDisplayText.GetMovedAnnouncement(action, 2, 4));
    }

    [Fact]
    public void GetRemovedAnnouncement_NamesTheAction()
    {
        var named = PieAction.CreateOpenAction("File Explorer", "explorer.exe");
        var unnamed = new PieAction { Name = " ", Type = ActionType.None };

        Assert.Equal("File Explorer removed.", ActionDisplayText.GetRemovedAnnouncement(named));
        Assert.Equal("Not set up removed.", ActionDisplayText.GetRemovedAnnouncement(unnamed));
    }
}
