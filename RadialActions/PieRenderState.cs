using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RadialActions;

internal sealed class PieRenderState
{
    public Duration HoverDuration { get; private set; } = new(TimeSpan.FromMilliseconds(100));
    public Duration PressDuration { get; private set; } = new(TimeSpan.FromMilliseconds(75));
    public IEasingFunction StandardEasing { get; private set; } = new QuadraticEase { EasingMode = EasingMode.EaseOut };

    public Color SliceColor { get; private set; } = SystemColors.ControlColor;
    public Color HoverColor { get; private set; } = SystemColors.ControlLightColor;
    public Color BorderColor { get; private set; } = SystemColors.ControlDarkColor;
    public Color BorderHoverColor { get; private set; } = SystemColors.ControlDarkColor;
    public Color HubColor { get; private set; } = SystemColors.ControlColor;
    public Color HubHoverColor { get; private set; } = SystemColors.ControlLightColor;
    public Color HubBorderColor { get; private set; } = SystemColors.ControlDarkColor;
    public Color CenterHoverBorderColor { get; private set; } = SystemColors.ControlDarkColor;

    public void ApplyTheme(PieThemeSnapshot theme)
    {
        HoverDuration = theme.HoverDuration;
        PressDuration = theme.PressDuration;
        StandardEasing = theme.StandardEasing;
        SliceColor = theme.SliceColor;
        HoverColor = theme.HoverColor;
        BorderColor = theme.BorderColor;
        BorderHoverColor = theme.BorderHoverColor;
        HubColor = theme.HubColor;
        HubHoverColor = theme.HubHoverColor;
        HubBorderColor = theme.HubBorderColor;
        CenterHoverBorderColor = theme.CenterHoverBorderColor;
    }
}
