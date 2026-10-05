using System.Windows;
using System.Windows.Media.Animation;

namespace RadialActions;

/// <summary>
/// Easing defined by a CSS-style cubic Bézier curve from (0,0) to (1,1), so Fluent motion curves such as cubic-bezier(0, 0, 0, 1) can be used directly.
/// </summary>
/// <remarks>
/// Derives from <see cref="EasingFunctionBase"/> so it stays freezable and can be used inside style and template storyboards.
/// <see cref="EasingFunctionBase.EasingMode"/> defaults to EaseIn here, which applies the curve as written; the base class default of EaseOut would mirror it.
/// </remarks>
public sealed class CubicBezierEase : EasingFunctionBase
{
    public static readonly DependencyProperty X1Property = RegisterControlPoint(nameof(X1), 0);
    public static readonly DependencyProperty Y1Property = RegisterControlPoint(nameof(Y1), 0);
    public static readonly DependencyProperty X2Property = RegisterControlPoint(nameof(X2), 1);
    public static readonly DependencyProperty Y2Property = RegisterControlPoint(nameof(Y2), 1);

    private KeySpline _spline;

    static CubicBezierEase()
    {
        EasingModeProperty.OverrideMetadata(typeof(CubicBezierEase), new PropertyMetadata(EasingMode.EaseIn));
    }

    public CubicBezierEase()
    {
    }

    public CubicBezierEase(double x1, double y1, double x2, double y2)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public double X1
    {
        get => (double)GetValue(X1Property);
        set => SetValue(X1Property, value);
    }

    public double Y1
    {
        get => (double)GetValue(Y1Property);
        set => SetValue(Y1Property, value);
    }

    public double X2
    {
        get => (double)GetValue(X2Property);
        set => SetValue(X2Property, value);
    }

    public double Y2
    {
        get => (double)GetValue(Y2Property);
        set => SetValue(Y2Property, value);
    }

    protected override double EaseInCore(double normalizedTime)
    {
        // KeySpline rejects control points outside the unit square, so overshooting curves are clamped rather than throwing mid-animation.
        _spline ??= new KeySpline(Math.Clamp(X1, 0, 1), Math.Clamp(Y1, 0, 1), Math.Clamp(X2, 0, 1), Math.Clamp(Y2, 0, 1));
        return _spline.GetSplineProgress(Math.Clamp(normalizedTime, 0, 1));
    }

    protected override Freezable CreateInstanceCore() => new CubicBezierEase();

    private static DependencyProperty RegisterControlPoint(string name, double defaultValue)
    {
        return DependencyProperty.Register(
            name,
            typeof(double),
            typeof(CubicBezierEase),
            new PropertyMetadata(defaultValue, (d, _) => ((CubicBezierEase)d)._spline = null));
    }
}
