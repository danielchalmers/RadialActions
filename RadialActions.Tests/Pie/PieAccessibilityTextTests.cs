namespace RadialActions.Tests;

public class PieAccessibilityTextTests
{
    [Fact]
    public void GetSliceName_UsesTheFullActionName()
    {
        var action = PieAction.CreateOpenAction("Remote Desktop Connection", "mstsc.exe", "*", null, null);

        Assert.Equal("Remote Desktop Connection", PieAccessibilityText.GetSliceName(action));
    }

    [Fact]
    public void GetSliceName_WithoutAName_FallsBackToTheTypeNameSettingsShows()
    {
        var action = new PieAction { Name = "  ", Type = ActionType.Open };

        Assert.Equal(ActionDisplayText.GetTypeName(ActionType.Open), PieAccessibilityText.GetSliceName(action));
    }

    [Theory]
    [InlineData(0, "1")]
    [InlineData(4, "5")]
    [InlineData(8, "9")]
    [InlineData(9, "")]
    [InlineData(-1, "")]
    public void GetAccessKey_MatchesTheNumberKeyThatRunsTheSlice(int index, string expectedKey)
    {
        Assert.Equal(expectedKey, PieAccessibilityText.GetAccessKey(index));
    }

    [Fact]
    public void DescribeSelection_IncludesNameAndPosition()
    {
        var action = new PieAction { Name = "Mute", Type = ActionType.Key };

        Assert.Equal("Mute, 2 of 5", PieAccessibilityText.DescribeSelection(action, 1, 5));
    }
}
