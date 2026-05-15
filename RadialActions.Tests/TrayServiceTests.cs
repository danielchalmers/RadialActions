using System.IO;

namespace RadialActions.Tests;

public class TrayServiceTests
{
    [Theory]
    [InlineData("No action configured", "No action configured")]
    [InlineData("Shortcut is invalid", "Shortcut is invalid")]
    public void GetActionFailureReason_InvalidOperation_ReturnsMessage(string exceptionMessage, string expected)
    {
        var reason = TrayService.GetActionFailureReason(new InvalidOperationException(exceptionMessage));

        Assert.Equal(expected, reason);
    }

    [Theory]
    [InlineData(typeof(FileNotFoundException), "Target was not found")]
    [InlineData(typeof(DirectoryNotFoundException), "Folder was not found")]
    [InlineData(typeof(UnauthorizedAccessException), "Access was denied")]
    [InlineData(typeof(NotSupportedException), "Target is not supported")]
    [InlineData(typeof(Exception), "Could not launch action")]
    public void GetActionFailureReason_KnownExceptionType_ReturnsFriendlyMessage(Type exceptionType, string expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "raw failure message")!;

        var reason = TrayService.GetActionFailureReason(exception);

        Assert.Equal(expected, reason);
    }
}
