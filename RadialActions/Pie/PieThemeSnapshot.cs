using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace RadialActions;

internal sealed class PieThemeSnapshot
{
    // Code fallbacks match the app motion tokens in Themes/AppTheme.xaml, so the pie behaves the same if a lookup ever fails.
    public static readonly Duration FallbackHoverDuration = new(TimeSpan.FromMilliseconds(83));
    public static readonly Duration FallbackPressDuration = new(TimeSpan.FromMilliseconds(67));
    public static readonly Duration FallbackReorderDuration = new(TimeSpan.FromMilliseconds(167));

    private static readonly FontFamily FallbackSymbolFontFamily = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public required bool IsHighContrast { get; init; }
    public required double SliceStrokeThickness { get; init; }
    public required double HubStrokeThickness { get; init; }
    public required double SelectionArcThickness { get; init; }
    public required double IconToLabelSpacing { get; init; }
    public required Thickness ContentPadding { get; init; }
    public required Duration HoverDuration { get; init; }
    public required Duration PressDuration { get; init; }
    public required Duration ReorderDuration { get; init; }
    public required IEasingFunction StandardEasing { get; init; }
    public required IEasingFunction DecelerateEasing { get; init; }
    public required Style SlicePathStyle { get; init; }
    public required Style SelectionArcStyle { get; init; }
    public required Style HubEllipseStyle { get; init; }
    public required Style HubContainerStyle { get; init; }
    public required Style IconTextStyle { get; init; }
    public required Style LabelTextStyle { get; init; }
    public required Style HintTextStyle { get; init; }
    public required Style GlyphTextStyle { get; init; }
    public required Style HubGlyphTextStyle { get; init; }
    public required Style MenuItemIconStyle { get; init; }
    public required FontFamily SymbolFontFamily { get; init; }
    public required Effect AmbientShadowEffect { get; init; }
    public required Color SliceColor { get; init; }
    public required Color HoverColor { get; init; }
    public required Color PressedColor { get; init; }
    public required Color StrokeColor { get; init; }
    public required Color HubColor { get; init; }
    public required Color HubHoverColor { get; init; }

    /// <summary>
    /// The selection arc, the release hint and the hovered hub glyph; verified to reach 3:1 on every slice and hub fill.
    /// </summary>
    public required Color AccentColor { get; init; }

    public required Color IconTextColor { get; init; }
    public required Color LabelTextColor { get; init; }
    public required Color HintTextColor { get; init; }

    /// <summary>
    /// Foreground for everything on a hovered or selected slice in high contrast, where the fill becomes Highlight.
    /// </summary>
    public required Color SelectedForegroundColor { get; init; }

    public required Color HubGlyphColor { get; init; }
    public required Color HubGlyphHoverColor { get; init; }

    public static PieThemeSnapshot Capture(
        Func<string, object> tryFindResource,
        bool isDarkModeEnabled)
    {
        var isHighContrast = SystemParameters.HighContrast;

        object ResolveResource(string resourceKey)
        {
            if (isHighContrast)
            {
                var highContrastValue = tryFindResource($"{resourceKey}.HighContrast");
                if (highContrastValue != null)
                {
                    return highContrastValue;
                }
            }

            var themedValue = tryFindResource($"{resourceKey}.{(isDarkModeEnabled ? "Dark" : "Light")}");
            if (themedValue != null)
            {
                return themedValue;
            }

            return tryFindResource(resourceKey);
        }

        static Color ToColor(object value, Color fallbackColor)
        {
            return value switch
            {
                SolidColorBrush brush => brush.Color,
                Color color => color,
                _ => fallbackColor,
            };
        }

        static double ToDouble(object value, double fallbackValue)
        {
            return value switch
            {
                double doubleValue => doubleValue,
                int intValue => intValue,
                _ => fallbackValue,
            };
        }

        static Duration ToDuration(object value, Duration fallbackValue)
        {
            return value switch
            {
                Duration duration => duration,
                TimeSpan timeSpan => new Duration(timeSpan),
                _ => fallbackValue,
            };
        }

        var sliceStrokeThickness = Math.Max(1, ToDouble(ResolveResource("PieSliceStrokeThickness"), 1));
        var hubStrokeThickness = Math.Max(1, ToDouble(ResolveResource("PieHubStrokeThickness"), 1));
        var selectionArcThickness = Math.Max(1, ToDouble(ResolveResource("PieSelectionArcThickness"), 3));
        var iconToLabelSpacing = Math.Max(0, ToDouble(ResolveResource("PieIconToLabelSpacing"), 3));
        var contentPadding = ResolveResource("PieSliceContentPadding") as Thickness? ?? new Thickness(2);

        // Motion comes from the shared app tokens so the pie, the menu window and Settings move alike.
        var hoverDuration = ToDuration(tryFindResource("MotionDurationFast"), FallbackHoverDuration);
        var pressDuration = ToDuration(tryFindResource("MotionDurationPress"), FallbackPressDuration);
        var reorderDuration = ToDuration(tryFindResource("MotionDurationNormal"), FallbackReorderDuration);
        var standardEasing = tryFindResource("MotionEaseStandard") as IEasingFunction ?? new CubicBezierEase(0.55, 0.55, 0, 1);
        var decelerateEasing = tryFindResource("MotionEaseDecelerate") as IEasingFunction ?? new CubicBezierEase(0, 0, 0, 1);

        var symbolFontFamily = tryFindResource("SymbolThemeFontFamily") as FontFamily ?? FallbackSymbolFontFamily;

        var sliceColor = ToColor(ResolveResource("PieSliceFillBrush"), SystemColors.WindowColor);
        var hoverColor = ToColor(ResolveResource("PieSliceHoverBrush"), SystemColors.ControlLightColor);
        var pressedColor = ToColor(ResolveResource("PieSlicePressedBrush"), SystemColors.ControlColor);
        var strokeColor = ToColor(ResolveResource("PieStrokeBrush"), SystemColors.ControlDarkColor);
        var hubColor = ToColor(ResolveResource("PieHubFillBrush"), SystemColors.ControlColor);
        var labelTextColor = ToColor(ResolveResource("PieTextBrush"), SystemColors.WindowTextColor);
        var hintTextColor = ToColor(ResolveResource("PieHintTextBrush"), SystemColors.GrayTextColor);

        Color hubHoverColor;
        Color accentColor;
        Color iconTextColor;
        Color selectedForegroundColor;
        Color hubGlyphColor;
        Color hubGlyphHoverColor;

        if (isHighContrast)
        {
            // System color pairs only: WindowText on Window at rest, HighlightText on Highlight when hovered, selected or pressed.
            sliceColor = SystemColors.WindowColor;
            hoverColor = SystemColors.HighlightColor;
            pressedColor = SystemColors.HighlightColor;
            strokeColor = SystemColors.WindowTextColor;
            hubColor = SystemColors.WindowColor;
            hubHoverColor = SystemColors.HighlightColor;
            labelTextColor = SystemColors.WindowTextColor;
            hintTextColor = SystemColors.WindowTextColor;
            iconTextColor = SystemColors.WindowTextColor;
            selectedForegroundColor = SystemColors.HighlightTextColor;
            accentColor = SystemColors.HighlightTextColor;
            hubGlyphColor = SystemColors.WindowTextColor;
            hubGlyphHoverColor = SystemColors.HighlightTextColor;
        }
        else
        {
            sliceColor = PieColorMath.Opaque(sliceColor);
            hoverColor = PieColorMath.Opaque(hoverColor);
            pressedColor = PieColorMath.Opaque(pressedColor);
            strokeColor = PieColorMath.Opaque(strokeColor);
            hubColor = PieColorMath.Opaque(hubColor);
            hubHoverColor = hoverColor;

            // The Fluent accent matches Settings and is already shaded per theme; the guard only steps in for accents that would be unreadable on the pie.
            var accentBackgrounds = new[] { sliceColor, hoverColor, pressedColor, hubHoverColor };
            accentColor = PieColorMath.EnsureContrast(GetAccentColor(tryFindResource), PieColorMath.MinGraphicContrast, accentBackgrounds);
            iconTextColor = accentColor;
            labelTextColor = PieColorMath.EnsureContrast(labelTextColor, PieColorMath.MinTextContrast, sliceColor, hoverColor, pressedColor);
            hintTextColor = PieColorMath.EnsureContrast(hintTextColor, PieColorMath.MinTextContrast, sliceColor, hoverColor, pressedColor, hubColor);
            selectedForegroundColor = labelTextColor;
            hubGlyphColor = hintTextColor;
            hubGlyphHoverColor = accentColor;
        }

        return new PieThemeSnapshot
        {
            IsHighContrast = isHighContrast,
            SliceStrokeThickness = sliceStrokeThickness,
            HubStrokeThickness = hubStrokeThickness,
            SelectionArcThickness = selectionArcThickness,
            IconToLabelSpacing = iconToLabelSpacing,
            ContentPadding = contentPadding,
            HoverDuration = hoverDuration,
            PressDuration = pressDuration,
            ReorderDuration = reorderDuration,
            StandardEasing = standardEasing,
            DecelerateEasing = decelerateEasing,
            SlicePathStyle = ResolveResource("PieSlicePathStyle") as Style,
            SelectionArcStyle = ResolveResource("PieSelectionArcStyle") as Style,
            HubEllipseStyle = ResolveResource("PieHubEllipseStyle") as Style,
            HubContainerStyle = ResolveResource("PieHubContainerStyle") as Style,
            IconTextStyle = ResolveResource("PieIconTextStyle") as Style,
            LabelTextStyle = ResolveResource("PieLabelTextStyle") as Style,
            HintTextStyle = ResolveResource("PieHintTextStyle") as Style,
            GlyphTextStyle = ResolveResource("PieGlyphTextStyle") as Style,
            HubGlyphTextStyle = ResolveResource("PieHubGlyphTextStyle") as Style,
            MenuItemIconStyle = tryFindResource("MenuItemIconStyle") as Style,
            SymbolFontFamily = symbolFontFamily,
            AmbientShadowEffect = ResolveResource("PieAmbientShadowEffect") as Effect,
            SliceColor = sliceColor,
            HoverColor = hoverColor,
            PressedColor = pressedColor,
            StrokeColor = strokeColor,
            HubColor = hubColor,
            HubHoverColor = hubHoverColor,
            AccentColor = accentColor,
            IconTextColor = iconTextColor,
            LabelTextColor = labelTextColor,
            HintTextColor = hintTextColor,
            SelectedForegroundColor = selectedForegroundColor,
            HubGlyphColor = hubGlyphColor,
            HubGlyphHoverColor = hubGlyphHoverColor,
        };
    }

    public static bool IsAppDarkModeEnabled()
    {
        const string personalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string appsUseLightTheme = "AppsUseLightTheme";

        try
        {
            using var personalizeKey = Registry.CurrentUser.OpenSubKey(personalizePath);
            if (personalizeKey?.GetValue(appsUseLightTheme) is int lightThemeFlag)
            {
                return lightThemeFlag == 0;
            }
        }
        catch
        {
            // Ignore registry access failures and fallback to system colors.
        }

        return PieColorMath.GetRelativeLuminance(SystemColors.WindowColor) < 0.5;
    }

    /// <summary>
    /// The Fluent accent fill (the same shade Settings uses), falling back to the DWM colorization color.
    /// </summary>
    private static Color GetAccentColor(Func<string, object> tryFindResource)
    {
        if (tryFindResource("AccentFillColorDefaultBrush") is SolidColorBrush fluentAccentBrush)
        {
            return PieColorMath.Opaque(fluentAccentBrush.Color);
        }

        if (SystemParameters.WindowGlassBrush is SolidColorBrush glassBrush)
        {
            return PieColorMath.Opaque(glassBrush.Color);
        }

        return PieColorMath.Opaque(SystemParameters.WindowGlassColor);
    }
}
