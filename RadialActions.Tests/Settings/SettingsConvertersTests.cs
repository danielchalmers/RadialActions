using System.Globalization;
using System.Windows;

namespace RadialActions.Tests;

public sealed class SettingsConvertersTests
{
    [Fact]
    public void ActionTypeNameConverter_ConvertsTypeAndFallsBackForOtherValues()
    {
        var converter = new ActionTypeNameConverter();

        Assert.Equal("Open", converter.Convert(ActionType.Open, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("Not set up", converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ActionListItemNameConverter_UsesNameTypeAndEnabledState()
    {
        var converter = new ActionListItemNameConverter();

        var enabled = converter.Convert(["Mute", ActionType.Key, true], typeof(string), null, CultureInfo.InvariantCulture);
        var hidden = converter.Convert(["Explorer", ActionType.Open, false], typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Mute, Key", enabled);
        Assert.Equal("Explorer, Open, hidden", hidden);
    }

    [Fact]
    public void TextJoinConverter_JoinsNonEmptyTextInOrder()
    {
        var converter = new TextJoinConverter();

        var withError = converter.Convert(["Couldn't register Ctrl+Alt+Space.", "Select the box and press a key combination."], typeof(string), null, CultureInfo.InvariantCulture);
        var withoutError = converter.Convert([null, "Select the box and press a key combination."], typeof(string), null, CultureInfo.InvariantCulture);
        var unset = converter.Convert([DependencyProperty.UnsetValue, " ", "Help."], typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Couldn't register Ctrl+Alt+Space. Select the box and press a key combination.", withError);
        Assert.Equal("Select the box and press a key combination.", withoutError);
        Assert.Equal("Help.", unset);
    }
}
