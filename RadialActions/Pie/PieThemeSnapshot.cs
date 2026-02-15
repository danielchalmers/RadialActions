using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace RadialActions;

internal sealed class PieThemeSnapshot
{
    public required bool IsHighContrast { get; init; }
    public required double SliceStrokeThickness { get; init; }
    public required double HubStrokeThickness { get; init; }
    public required double IconToLabelSpacing { get; init; }
    public required double ContentMaxWidthRatio { get; init; }
    public required Thickness ContentPadding { get; init; }
    public required Duration HoverDuration { get; init; }
    public required Duration PressDuration { get; init; }
    public required IEasingFunction StandardEasing { get; init; }
    public required Style SlicePathStyle { get; init; }
    public required Style HubEllipseStyle { get; init; }
    public required Style HubContainerStyle { get; init; }
    public required Style IconTextStyle { get; init; }
    public required Style LabelTextStyle { get; init; }
    public required Effect AmbientShadowEffect { get; init; }
    public required Color SurfaceColor { get; init; }
    public required Color SurfaceBorderColor { get; init; }
    public required Color SliceColor { get; init; }
    public required Color HoverColor { get; init; }
    public required Color PressedColor { get; init; }
    public required Color BorderColor { get; init; }
    public required Color BorderHoverColor { get; init; }
    public required Color HubColor { get; init; }
    public required Color HubHoverColor { get; init; }
    public required Color HubBorderColor { get; init; }
    public required Color CenterHoverBorderColor { get; init; }
    public required Color IconTextColor { get; init; }
    public required Color LabelTextColor { get; init; }

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

        static SolidColorBrush ToSolidColorBrush(object value, Color fallbackColor)
        {
            if (value is SolidColorBrush brush)
            {
                return brush;
            }

            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }

            return new SolidColorBrush(fallbackColor);
        }

        static double ToDouble(object value, double fallbackValue)
        {
            if (value is double doubleValue)
            {
                return doubleValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            return fallbackValue;
        }

        static Duration ToDuration(object value, Duration fallbackValue)
        {
            if (value is Duration duration)
            {
                return duration;
            }

            if (value is TimeSpan timeSpan)
            {
                return new Duration(timeSpan);
            }

            return fallbackValue;
        }

        var sliceStrokeThickness = Math.Max(1, ToDouble(ResolveResource("PieSliceStrokeThickness"), 1.5));
        var hubStrokeThickness = Math.Max(1, ToDouble(ResolveResource("PieHubStrokeThickness"), 1.5));
        var iconToLabelSpacing = Math.Max(0, ToDouble(ResolveResource("PieIconToLabelSpacing"), 3));
        var contentMaxWidthRatio = Math.Clamp(ToDouble(ResolveResource("PieSliceContentMaxWidthRatio"), 0.38), 0.2, 0.8);
        var contentPadding = ResolveResource("PieSliceContentPadding") as Thickness? ?? new Thickness(2);

        var hoverDuration = ToDuration(ResolveResource("PieHoverDuration"), new Duration(TimeSpan.FromMilliseconds(100)));
        var pressDuration = ToDuration(ResolveResource("PiePressDuration"), new Duration(TimeSpan.FromMilliseconds(75)));
        var standardEasing = ResolveResource("PieEaseStandard") as IEasingFunction
            ?? new QuadraticEase { EasingMode = EasingMode.EaseOut };

        var slicePathStyle = ResolveResource("PieSlicePathStyle") as Style;
        var hubEllipseStyle = ResolveResource("PieHubEllipseStyle") as Style;
        var hubContainerStyle = ResolveResource("PieHubContainerStyle") as Style;
        var iconTextStyle = ResolveResource("PieIconTextStyle") as Style;
        var labelTextStyle = ResolveResource("PieLabelTextStyle") as Style;
        var ambientShadowEffect = ResolveResource("PieAmbientShadowEffect") as Effect;

        var accentColor = GetSystemAccentColor();
        var surfaceColor = ToSolidColorBrush(ResolveResource("PieSurfaceBrush"), SystemColors.ControlColor).Color;
        var surfaceBorderColor = ToSolidColorBrush(ResolveResource("PieSurfaceBorderBrush"), SystemColors.ControlDarkColor).Color;
        var sliceColor = ToSolidColorBrush(ResolveResource("PieSliceFillBrush"), SystemColors.ControlColor).Color;
        var hoverColor = ToSolidColorBrush(ResolveResource("PieSliceHoverBrush"), SystemColors.ControlLightColor).Color;
        var pressedColor = ToSolidColorBrush(ResolveResource("PieSlicePressedBrush"), SystemColors.ControlDarkColor).Color;
        var borderColor = ToSolidColorBrush(ResolveResource("PieSliceBorderBrush"), SystemColors.ControlDarkColor).Color;
        var hubColor = ToSolidColorBrush(ResolveResource("PieHubFillBrush"), SystemColors.ControlColor).Color;
        var hubHoverColor = ToSolidColorBrush(ResolveResource("PieHubHoverBrush"), SystemColors.ControlLightColor).Color;
        var hubBorderColor = ToSolidColorBrush(ResolveResource("PieHubBorderBrush"), SystemColors.ControlDarkColor).Color;
        var iconTextColor = accentColor;
        var labelTextColor = ToSolidColorBrush(ResolveResource("PieTextBrush"), SystemColors.WindowTextColor).Color;

        if (isHighContrast)
        {
            surfaceColor = SystemColors.WindowColor;
            surfaceBorderColor = SystemColors.WindowTextColor;
            sliceColor = SystemColors.WindowColor;
            hoverColor = SystemColors.HighlightColor;
            pressedColor = BlendColor(SystemColors.HighlightColor, SystemColors.WindowColor, 0.35);
            borderColor = SystemColors.WindowTextColor;
            hubColor = SystemColors.ControlColor;
            hubHoverColor = SystemColors.HighlightColor;
            hubBorderColor = SystemColors.WindowTextColor;
            iconTextColor = SystemColors.WindowTextColor;
            labelTextColor = SystemColors.WindowTextColor;
            accentColor = SystemColors.HighlightColor;
        }
        else
        {
            accentColor = GetAccessibleAccentColor(accentColor, sliceColor);
            iconTextColor = GetAccessibleAccentColor(iconTextColor, sliceColor);
            labelTextColor = GetAccessibleAccentColor(labelTextColor, sliceColor);
        }

        return new PieThemeSnapshot
        {
            IsHighContrast = isHighContrast,
            SliceStrokeThickness = sliceStrokeThickness,
            HubStrokeThickness = hubStrokeThickness,
            IconToLabelSpacing = iconToLabelSpacing,
            ContentMaxWidthRatio = contentMaxWidthRatio,
            ContentPadding = contentPadding,
            HoverDuration = hoverDuration,
            PressDuration = pressDuration,
            StandardEasing = standardEasing,
            SlicePathStyle = slicePathStyle,
            HubEllipseStyle = hubEllipseStyle,
            HubContainerStyle = hubContainerStyle,
            IconTextStyle = iconTextStyle,
            LabelTextStyle = labelTextStyle,
            AmbientShadowEffect = ambientShadowEffect,
            SurfaceColor = surfaceColor,
            SurfaceBorderColor = surfaceBorderColor,
            SliceColor = sliceColor,
            HoverColor = hoverColor,
            PressedColor = pressedColor,
            BorderColor = borderColor,
            BorderHoverColor = BlendColor(borderColor, accentColor, 0.40),
            HubColor = hubColor,
            HubHoverColor = hubHoverColor,
            HubBorderColor = hubBorderColor,
            CenterHoverBorderColor = BlendColor(hubBorderColor, accentColor, 0.45),
            IconTextColor = iconTextColor,
            LabelTextColor = labelTextColor,
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

        return GetRelativeLuminance(SystemColors.WindowColor) < 0.5;
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var r = (byte)Math.Round((from.R * (1 - amount)) + (to.R * amount));
        var g = (byte)Math.Round((from.G * (1 - amount)) + (to.G * amount));
        var b = (byte)Math.Round((from.B * (1 - amount)) + (to.B * amount));
        return Color.FromRgb(r, g, b);
    }

    private static Color GetSystemAccentColor()
    {
        if (SystemParameters.WindowGlassBrush is SolidColorBrush accentBrush)
        {
            return accentBrush.Color;
        }

        return SystemParameters.WindowGlassColor;
    }

    private static Color GetAccessibleAccentColor(Color accentColor, Color backgroundColor)
    {
        var contrast = GetContrastRatio(accentColor, backgroundColor);
        if (contrast >= 3.0)
        {
            return accentColor;
        }

        var isDarkBackground = GetRelativeLuminance(backgroundColor) < 0.5;
        var target = isDarkBackground ? Colors.White : Colors.Black;
        return BlendColor(accentColor, target, 0.35);
    }

    private static double GetContrastRatio(Color foreground, Color background)
    {
        var foregroundLuminance = GetRelativeLuminance(foreground);
        var backgroundLuminance = GetRelativeLuminance(background);
        var brighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (brighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(Color color)
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
}
