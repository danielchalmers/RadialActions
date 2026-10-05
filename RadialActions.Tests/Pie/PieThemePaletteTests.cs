using System.IO;
using System.Windows.Media;
using System.Xml.Linq;

namespace RadialActions.Tests;

/// <summary>
/// Guards the contrast promises of the pie palette in PieTheme.xaml, so a retuned color can't quietly make labels or hints unreadable, and the base sizes that code scales with the pie.
/// </summary>
public class PieThemePaletteTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Lazy<IReadOnlyDictionary<string, Color>> PaletteColors = new(LoadPaletteColors);

    public static TheoryData<string> Themes => new() { "Light", "Dark" };

    [Theory]
    [MemberData(nameof(Themes))]
    public void LabelText_ReachesTextContrastOnEverySliceFill(string theme)
    {
        var text = GetColor("PieTextBrush", theme);

        foreach (var fill in GetSliceFills(theme))
        {
            Assert.True(PieColorMath.GetContrastRatio(text, fill) >= PieColorMath.MinTextContrast, $"PieTextBrush.{theme} on {fill}");
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void HintText_ReachesTextContrastOnSliceFillsAndHub(string theme)
    {
        var hint = GetColor("PieHintTextBrush", theme);

        foreach (var fill in GetSliceFills(theme).Append(GetColor("PieHubFillBrush", theme)))
        {
            Assert.True(PieColorMath.GetContrastRatio(hint, fill) >= PieColorMath.MinTextContrast, $"PieHintTextBrush.{theme} on {fill}");
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void PressedFill_IsCalmerThanHover(string theme)
    {
        var rest = GetColor("PieSliceFillBrush", theme);
        var hover = GetColor("PieSliceHoverBrush", theme);
        var pressed = GetColor("PieSlicePressedBrush", theme);

        Assert.True(PieColorMath.GetContrastRatio(rest, pressed) < PieColorMath.GetContrastRatio(rest, hover));
    }

    [Theory]
    [InlineData("Light", "#FFD30050")]
    [InlineData("Light", "#FF005FB8")]
    [InlineData("Dark", "#FFFF529F")]
    [InlineData("Dark", "#FF60CDFF")]
    public void FluentAccents_NeedNoAdjustmentOnThePalette(string theme, string accentHex)
    {
        var accent = (Color)ColorConverter.ConvertFromString(accentHex);

        var result = PieColorMath.EnsureContrast(accent, PieColorMath.MinGraphicContrast, [.. GetSliceFills(theme)]);

        Assert.Equal(accent, result);
    }

    [Fact]
    public void HubGlyph_UsesADesignSystemGlyphSizeBasedOnTheSharedGlyphStyle()
    {
        var hubGlyphStyle = FindStyle("PieHubGlyphTextStyle");

        Assert.Equal("{StaticResource PieGlyphTextStyle}", (string)hubGlyphStyle.Attribute("BasedOn"));
        Assert.Equal("16", GetSetterValue(hubGlyphStyle, "FontSize"));
    }

    private static XElement FindStyle(string key)
    {
        var style = XDocument.Load(FindPieThemePath()).Root
            .Elements()
            .SingleOrDefault(element => element.Name.LocalName == "Style" && (string)element.Attribute(XamlNamespace + "Key") == key);
        Assert.True(style != null, $"{key} is missing from PieTheme.xaml");
        return style;
    }

    private static string GetSetterValue(XElement style, string property)
    {
        return style.Elements()
            .Where(element => element.Name.LocalName == "Setter" && (string)element.Attribute("Property") == property)
            .Select(element => (string)element.Attribute("Value"))
            .SingleOrDefault();
    }

    private static IEnumerable<Color> GetSliceFills(string theme)
    {
        yield return GetColor("PieSliceFillBrush", theme);
        yield return GetColor("PieSliceHoverBrush", theme);
        yield return GetColor("PieSlicePressedBrush", theme);
    }

    private static Color GetColor(string key, string theme)
    {
        var themedKey = $"{key}.{theme}";
        Assert.True(PaletteColors.Value.TryGetValue(themedKey, out var color), $"{themedKey} is missing from PieTheme.xaml");
        return color;
    }

    private static IReadOnlyDictionary<string, Color> LoadPaletteColors()
    {
        var document = XDocument.Load(FindPieThemePath());
        return document.Root
            .Elements()
            .Where(element => element.Name.LocalName == "SolidColorBrush")
            .ToDictionary(
                element => (string)element.Attribute(XamlNamespace + "Key"),
                element => (Color)ColorConverter.ConvertFromString((string)element.Attribute("Color")));
    }

    private static string FindPieThemePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "RadialActions", "Pie", "PieTheme.xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Couldn't find RadialActions/Pie/PieTheme.xaml above the test output directory.");
    }
}
