using System.Windows.Media;

namespace RadialActions;

/// <summary>
/// Pure color math for the pie palette: WCAG contrast ratios and a guard that nudges a color until it stays readable on every background it is drawn on.
/// </summary>
internal static class PieColorMath
{
    // WCAG 1.4.11 for glyphs and indicators, 1.4.3 for text.
    public const double MinGraphicContrast = 3.0;
    public const double MinTextContrast = 4.5;

    private const double BlendStep = 0.05;

    /// <summary>
    /// Drops the alpha channel, since the pie draws on opaque fills and contrast is computed on RGB only.
    /// </summary>
    public static Color Opaque(Color color) => Color.FromRgb(color.R, color.G, color.B);

    public static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var r = (byte)Math.Round((from.R * (1 - amount)) + (to.R * amount));
        var g = (byte)Math.Round((from.G * (1 - amount)) + (to.G * amount));
        var b = (byte)Math.Round((from.B * (1 - amount)) + (to.B * amount));
        return Color.FromRgb(r, g, b);
    }

    public static double GetRelativeLuminance(Color color)
    {
        static double ChannelToLinear(byte channel)
        {
            var srgb = channel / 255.0;
            return srgb <= 0.03928
                ? srgb / 12.92
                : Math.Pow((srgb + 0.055) / 1.055, 2.4);
        }

        var r = ChannelToLinear(color.R);
        var g = ChannelToLinear(color.G);
        var b = ChannelToLinear(color.B);
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    public static double GetContrastRatio(Color foreground, Color background)
    {
        var foregroundLuminance = GetRelativeLuminance(foreground);
        var backgroundLuminance = GetRelativeLuminance(background);
        var brighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (brighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Returns <paramref name="color"/> unchanged when it reaches <paramref name="minRatio"/> against every background; otherwise blends it toward black or white in small steps until it does.
    /// </summary>
    /// <remarks>
    /// Each step is re-checked against all backgrounds, so the result is verified rather than assumed. If neither direction gets there, the more readable of black and white is returned.
    /// </remarks>
    public static Color EnsureContrast(Color color, double minRatio, params Color[] backgrounds)
    {
        color = Opaque(color);
        if (backgrounds.Length == 0 || GetMinimumContrast(color, backgrounds) >= minRatio)
        {
            return color;
        }

        var steps = (int)Math.Round(1 / BlendStep);
        for (var step = 1; step <= steps; step++)
        {
            var towardBlack = Blend(color, Colors.Black, step * BlendStep);
            var towardWhite = Blend(color, Colors.White, step * BlendStep);
            var blackContrast = GetMinimumContrast(towardBlack, backgrounds);
            var whiteContrast = GetMinimumContrast(towardWhite, backgrounds);

            if (blackContrast >= minRatio || whiteContrast >= minRatio)
            {
                return blackContrast >= whiteContrast ? towardBlack : towardWhite;
            }
        }

        return GetMinimumContrast(Colors.Black, backgrounds) >= GetMinimumContrast(Colors.White, backgrounds)
            ? Colors.Black
            : Colors.White;
    }

    public static double GetMinimumContrast(Color foreground, IEnumerable<Color> backgrounds)
    {
        var minimum = double.MaxValue;
        foreach (var background in backgrounds)
        {
            minimum = Math.Min(minimum, GetContrastRatio(foreground, Opaque(background)));
        }

        return minimum;
    }
}
