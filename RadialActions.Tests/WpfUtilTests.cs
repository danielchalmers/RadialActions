using System.Windows;

namespace RadialActions.Tests;

public class WpfUtilTests
{
    [Theory]
    [InlineData(400, 1.0, 400)]
    [InlineData(400, 1.5, 600)]
    [InlineData(455.5, 1.5, 683)]
    public void ScaleDipToPixels_UsesMonitorScale(double dipValue, double dpiScale, int expectedPixels)
    {
        var pixels = WpfUtil.ScaleDipToPixels(dipValue, dpiScale);

        Assert.Equal(expectedPixels, pixels);
    }

    [Fact]
    public void CalculateCenteredPositionInDevicePixels_CentersMenuUsingScaledWindowSize()
    {
        var position = WpfUtil.CalculateCenteredPositionInDevicePixels(
            new Point(1152, 768),
            new Rect(0, 0, 2304, 1536),
            new Size(456, 456),
            new Point(1.5, 1.5));

        Assert.Equal(new Point(810, 426), position);
    }

    [Fact]
    public void CalculateCenteredPositionInDevicePixels_ClampsToBounds()
    {
        var position = WpfUtil.CalculateCenteredPositionInDevicePixels(
            new Point(20, 20),
            new Rect(0, 0, 1920, 1080),
            new Size(400, 400),
            new Point(1, 1));

        Assert.Equal(new Point(0, 0), position);
    }
}
