namespace RadialActions.Tests;

public class StartupRegistrationTests
{
    private const string ExePath = @"C:\Users\Jane Doe\AppData\Local\RadialActions\RadialActions\RadialActions.exe";

    [Fact]
    public void FormatCommand_QuotesThePath()
    {
        Assert.Equal($"\"{ExePath}\"", StartupRegistration.FormatCommand(ExePath));
    }

    [Theory]
    [InlineData("\"" + ExePath + "\"")]
    [InlineData(ExePath)]
    [InlineData("  \"" + ExePath + "\"  ")]
    [InlineData("\"" + ExePath + "\" --background")]
    [InlineData(@"""c:\users\jane doe\appdata\local\radialactions\radialactions\radialactions.exe""")]
    [InlineData("\"" + ExePath)]
    public void PointsAt_ThisExeQuotedOrAsEarlierVersionsWroteIt_ReturnsTrue(string command)
    {
        Assert.True(StartupRegistration.PointsAt(command, ExePath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"""D:\Portable\RadialActions\RadialActions.exe""")]
    [InlineData(@"D:\Portable\RadialActions\RadialActions.exe")]
    public void PointsAt_AnotherExeOrNothing_ReturnsFalse(string command)
    {
        Assert.False(StartupRegistration.PointsAt(command, ExePath));
    }

    [Fact]
    public void FormatCommand_RoundTripsThroughPointsAt()
    {
        Assert.True(StartupRegistration.PointsAt(StartupRegistration.FormatCommand(ExePath), ExePath));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(new byte[0], false)]
    [InlineData(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 0x03, 0, 0, 0, 0x10, 0x2A, 0x4B, 0x1C, 0x9E, 0x35, 0xDB, 0x01 }, true)]
    [InlineData(new byte[] { 0x07, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
    public void IsDisabledInTaskManager_ReadsTheFirstByte(byte[] startupApprovedValue, bool expected)
    {
        Assert.Equal(expected, StartupRegistration.IsDisabledInTaskManager(startupApprovedValue));
    }
}
