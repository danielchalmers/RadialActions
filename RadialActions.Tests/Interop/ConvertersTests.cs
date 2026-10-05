using System.Globalization;
using System.Windows;

namespace RadialActions.Tests;

public class ConvertersTests
{
    [Theory]
    [InlineData(true, false, Visibility.Visible)]
    [InlineData(false, false, Visibility.Collapsed)]
    [InlineData(true, true, Visibility.Collapsed)]
    [InlineData(false, true, Visibility.Visible)]
    public void BooleanToVisibility_HonorsInversion(bool value, bool isInverted, Visibility expected)
    {
        var converter = new BooleanToVisibilityConverter { IsInverted = isInverted };

        Assert.Equal(expected, converter.Convert(value, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(value, converter.ConvertBack(expected, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BooleanToVisibility_NonBooleanIsTreatedAsFalse()
    {
        Assert.Equal(Visibility.Collapsed, new BooleanToVisibilityConverter().Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, new BooleanToVisibilityConverter { IsInverted = true }.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture));
    }
}
