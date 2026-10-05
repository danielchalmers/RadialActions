using System.Windows.Media;

namespace RadialActions.Tests;

public class PieColorMathTests
{
    private static readonly Color LightSlice = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color LightHover = Color.FromRgb(0xF0, 0xF0, 0xF0);
    private static readonly Color DarkSlice = Color.FromRgb(0x2C, 0x2C, 0x2C);
    private static readonly Color DarkHover = Color.FromRgb(0x3B, 0x3B, 0x3B);

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    [Fact]
    public void GetContrastRatio_BlackOnWhite_IsTwentyOne()
    {
        Assert.Equal(21, PieColorMath.GetContrastRatio(Colors.Black, Colors.White), precision: 6);
        Assert.Equal(1, PieColorMath.GetContrastRatio(LightHover, LightHover), precision: 6);
    }

    [Theory]
    [InlineData("#FFD30050", false)]
    [InlineData("#FF005FB8", false)]
    [InlineData("#FFFF529F", true)]
    [InlineData("#FF60CDFF", true)]
    public void EnsureContrast_ReadableFluentAccents_AreLeftUnchanged(string accentHex, bool isDark)
    {
        var accent = Parse(accentHex);
        Color[] backgrounds = isDark ? [DarkSlice, DarkHover] : [LightSlice, LightHover];

        var result = PieColorMath.EnsureContrast(accent, PieColorMath.MinGraphicContrast, backgrounds);

        Assert.Equal(accent, result);
    }

    [Theory]
    [InlineData("#FF000080", true)]
    [InlineData("#FF0A2463", true)]
    [InlineData("#FFFFFF80", false)]
    [InlineData("#FFD50056", true)]
    public void EnsureContrast_UnreadableAccents_ReachTheTargetOnEveryBackground(string accentHex, bool isDark)
    {
        var accent = Parse(accentHex);
        Color[] backgrounds = isDark ? [DarkSlice, DarkHover] : [LightSlice, LightHover];
        Assert.True(PieColorMath.GetMinimumContrast(accent, backgrounds) < PieColorMath.MinGraphicContrast);

        var result = PieColorMath.EnsureContrast(accent, PieColorMath.MinGraphicContrast, backgrounds);

        Assert.NotEqual(accent, result);
        Assert.True(PieColorMath.GetMinimumContrast(result, backgrounds) >= PieColorMath.MinGraphicContrast);
    }

    [Fact]
    public void EnsureContrast_UsesTheTextTargetWhenAsked()
    {
        var grey = Parse("#FF8A8A8A");

        var result = PieColorMath.EnsureContrast(grey, PieColorMath.MinTextContrast, LightSlice, LightHover);

        Assert.True(PieColorMath.GetMinimumContrast(result, [LightSlice, LightHover]) >= PieColorMath.MinTextContrast);
    }

    [Fact]
    public void EnsureContrast_DropsAlpha()
    {
        var translucent = Color.FromArgb(0x80, 0xD3, 0x00, 0x50);

        var result = PieColorMath.EnsureContrast(translucent, PieColorMath.MinGraphicContrast, LightSlice);

        Assert.Equal(0xFF, result.A);
        Assert.Equal(Color.FromRgb(0xD3, 0x00, 0x50), result);
    }

    [Fact]
    public void EnsureContrast_UnreachableTarget_ReturnsTheMoreReadableExtreme()
    {
        var midGrey = Color.FromRgb(0x80, 0x80, 0x80);

        var result = PieColorMath.EnsureContrast(Colors.Red, 21, midGrey);

        Assert.Equal(Colors.Black, result);
    }
}
