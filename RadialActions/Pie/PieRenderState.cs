using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RadialActions;

internal sealed class PieRenderState
{
    public bool IsHighContrast { get; private set; }
    public Duration HoverDuration { get; private set; } = PieThemeSnapshot.FallbackHoverDuration;
    public Duration PressDuration { get; private set; } = PieThemeSnapshot.FallbackPressDuration;
    public Duration ReorderDuration { get; private set; } = PieThemeSnapshot.FallbackReorderDuration;
    public IEasingFunction StandardEasing { get; private set; } = new CubicBezierEase(0.55, 0.55, 0, 1);
    public IEasingFunction DecelerateEasing { get; private set; } = new CubicBezierEase(0, 0, 0, 1);

    public Color SliceColor { get; private set; } = SystemColors.WindowColor;
    public Color HoverColor { get; private set; } = SystemColors.HighlightColor;
    public Color PressedColor { get; private set; } = SystemColors.HighlightColor;
    public Color HubColor { get; private set; } = SystemColors.WindowColor;
    public Color HubHoverColor { get; private set; } = SystemColors.HighlightColor;
    public Color ForegroundColor { get; private set; } = SystemColors.WindowTextColor;
    public Color SelectedForegroundColor { get; private set; } = SystemColors.HighlightTextColor;
    public Color HubGlyphColor { get; private set; } = SystemColors.WindowTextColor;
    public Color HubGlyphHoverColor { get; private set; } = SystemColors.HighlightTextColor;

    public void ApplyTheme(PieThemeSnapshot theme)
    {
        IsHighContrast = theme.IsHighContrast;
        HoverDuration = theme.HoverDuration;
        PressDuration = theme.PressDuration;
        ReorderDuration = theme.ReorderDuration;
        StandardEasing = theme.StandardEasing;
        DecelerateEasing = theme.DecelerateEasing;
        SliceColor = theme.SliceColor;
        HoverColor = theme.HoverColor;
        PressedColor = theme.PressedColor;
        HubColor = theme.HubColor;
        HubHoverColor = theme.HubHoverColor;
        ForegroundColor = theme.LabelTextColor;
        SelectedForegroundColor = theme.SelectedForegroundColor;
        HubGlyphColor = theme.HubGlyphColor;
        HubGlyphHoverColor = theme.HubGlyphHoverColor;
    }
}
