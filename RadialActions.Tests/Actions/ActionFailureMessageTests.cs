using System.ComponentModel;
using System.IO;

namespace RadialActions.Tests;

public class ActionFailureMessageTests
{
    [Fact]
    public void Create_NamesTheActionInTheTitle()
    {
        var action = PieAction.CreateOpenAction("Explorer", "explorer.exe");

        var notification = ActionFailureMessage.Create(action, new Win32Exception(2));

        Assert.Equal("Couldn't run Explorer", notification.Title);
    }

    [Fact]
    public void Create_UserCancelledUac_ShowsNothing()
    {
        var action = PieAction.CreateOpenAction("Admin tool", "tool.exe");

        Assert.Null(ActionFailureMessage.Create(action, new Win32Exception(1223)));
    }

    [Theory]
    [InlineData(2, "Windows can't find the target. Check that it still exists.")]
    [InlineData(3, "Windows can't find part of the path. Check the target and the working directory.")]
    [InlineData(5, "Windows denied access to the target.")]
    [InlineData(267, "The working directory doesn't exist.")]
    [InlineData(1155, "No app is set up to open this type of file.")]
    [InlineData(1450, "Something went wrong. Check the action's settings.")]
    public void Create_OpenAction_MapsWindowsErrors(int errorCode, string expected)
    {
        var action = PieAction.CreateOpenAction("Docs", @"C:\Missing\Docs.txt");

        var notification = ActionFailureMessage.Create(action, new Win32Exception(errorCode));

        Assert.Equal(expected, notification.Message);
    }

    [Theory]
    [InlineData("", "Windows can't find powershell.exe.")]
    [InlineData("pwsh.exe", "Windows can't find pwsh.exe.")]
    public void Create_ScriptWithMissingInterpreter_NamesTheInterpreter(string interpreter, string expected)
    {
        var action = PieAction.CreateScriptAction("Backup", "Get-Date", interpreter: interpreter);

        var notification = ActionFailureMessage.Create(action, new Win32Exception(2));

        Assert.Equal(expected, notification.Message);
    }

    [Theory]
    [InlineData("No action configured", "No action configured.")]
    [InlineData("This action doesn't have a type yet.", "This action doesn't have a type yet.")]
    [InlineData("  Script is empty  ", "Script is empty.")]
    public void Create_InvalidOperation_KeepsTheMessageAsASentence(string message, string expected)
    {
        var notification = ActionFailureMessage.Create(new PieAction("Blank"), new InvalidOperationException(message));

        Assert.Equal(expected, notification.Message);
    }

    [Fact]
    public void Create_ManagedFileExceptions_MatchTheWindowsErrors()
    {
        var action = PieAction.CreateOpenAction("Docs", "docs.txt");

        Assert.Equal(
            ActionFailureMessage.Create(action, new Win32Exception(2)).Message,
            ActionFailureMessage.Create(action, new FileNotFoundException()).Message);
        Assert.Equal(
            ActionFailureMessage.Create(action, new Win32Exception(3)).Message,
            ActionFailureMessage.Create(action, new DirectoryNotFoundException()).Message);
        Assert.Equal(
            ActionFailureMessage.Create(action, new Win32Exception(5)).Message,
            ActionFailureMessage.Create(action, new UnauthorizedAccessException()).Message);
    }

    [Fact]
    public void Create_UnknownException_UsesAGenericSentence()
    {
        var notification = ActionFailureMessage.Create(new PieAction("Odd"), new FormatException("bad"));

        Assert.Equal("Something went wrong. Check the action's settings.", notification.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetTitle_WithoutName_FallsBackToTheAction(string name)
    {
        Assert.Equal("Couldn't run the action", ActionFailureMessage.GetTitle(name));
    }

    [Fact]
    public void GetTitle_LongName_IsShortenedToFitWindowsLimit()
    {
        var title = ActionFailureMessage.GetTitle(new string('a', 100));

        Assert.Equal(ActionFailureMessage.MaxTitleLength, title.Length);
        Assert.StartsWith("Couldn't run aaa", title);
        Assert.EndsWith("a…", title);
    }

    [Fact]
    public void GetTitle_LongName_DoesNotSplitAnEmoji()
    {
        // The cut lands between the halves of the surrogate pair, so the whole emoji must be dropped.
        var name = new string('a', 48) + "😀" + new string('b', 20);

        var title = ActionFailureMessage.GetTitle(name);

        Assert.Equal("Couldn't run " + new string('a', 48) + "…", title);
    }

    [Fact]
    public void GetTitle_NameThatFits_IsUnchanged()
    {
        var name = new string('a', ActionFailureMessage.MaxTitleLength - "Couldn't run ".Length);

        Assert.Equal("Couldn't run " + name, ActionFailureMessage.GetTitle(name));
    }
}
