using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RadialActions;

internal sealed class PieAnimationService
{
    public void ApplyBrushColor(
        SolidColorBrush brush,
        Color color,
        bool animate,
        Duration duration,
        IEasingFunction easingFunction)
    {
        if (animate)
        {
            AnimateBrushColor(brush, color, duration, easingFunction);
            return;
        }

        brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        brush.Color = color;
    }

    private static void AnimateBrushColor(
        SolidColorBrush brush,
        Color toColor,
        Duration duration,
        IEasingFunction easingFunction)
    {
        if (IsReducedMotionEnabled())
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = toColor;
            return;
        }

        var colorAnimation = new ColorAnimation
        {
            To = toColor,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    public void ApplyOpacity(
        UIElement element,
        double opacity,
        bool animate,
        Duration duration,
        IEasingFunction easingFunction)
    {
        if (animate)
        {
            AnimateOpacity(element, opacity, duration, easingFunction);
            return;
        }

        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = opacity;
    }

    private static void AnimateOpacity(
        UIElement element,
        double toOpacity,
        Duration duration,
        IEasingFunction easingFunction)
    {
        if (IsReducedMotionEnabled())
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = toOpacity;
            return;
        }

        var opacityAnimation = new DoubleAnimation
        {
            To = toOpacity,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    public void AnimateRotationAngle(
        RotateTransform transform,
        double toAngle,
        Duration duration,
        IEasingFunction easingFunction,
        Action onCompleted = null)
    {
        if (IsReducedMotionEnabled())
        {
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
            transform.Angle = toAngle;
            onCompleted?.Invoke();
            return;
        }

        var rotationAnimation = new DoubleAnimation
        {
            To = toAngle,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        if (onCompleted != null)
        {
            rotationAnimation.Completed += (_, _) => onCompleted();
        }

        transform.BeginAnimation(RotateTransform.AngleProperty, rotationAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    public void AnimateClickDown(UIElement target, double pressedScale, Duration duration, IEasingFunction easingFunction)
    {
        var scaleTransform = FindScaleTransform(target);
        if (scaleTransform == null)
        {
            scaleTransform = new ScaleTransform(1, 1);
            target.RenderTransform = scaleTransform;
            target.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        if (IsReducedMotionEnabled())
        {
            SetScale(scaleTransform, pressedScale);
            return;
        }

        var scaleAnimation = new DoubleAnimation
        {
            To = pressedScale,
            Duration = duration,
            EasingFunction = easingFunction,
            FillBehavior = FillBehavior.HoldEnd,
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    public void AnimateClickUp(UIElement target, Duration duration, IEasingFunction easingFunction)
    {
        var scaleTransform = FindScaleTransform(target);
        if (scaleTransform == null)
        {
            return;
        }

        if (IsReducedMotionEnabled())
        {
            SetScale(scaleTransform, 1);
            return;
        }

        var scaleAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Snaps a press scale back to 1 without animating, so a press interrupted by a dismiss never carries into the next open.
    /// </summary>
    public void ResetClickScale(UIElement target)
    {
        var scaleTransform = FindScaleTransform(target);
        if (scaleTransform != null)
        {
            SetScale(scaleTransform, 1);
        }
    }

    public static bool IsReducedMotionEnabled()
    {
        return !SystemParameters.ClientAreaAnimation;
    }

    private static void SetScale(ScaleTransform scaleTransform, double scale)
    {
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scaleTransform.ScaleX = scale;
        scaleTransform.ScaleY = scale;
    }

    private static ScaleTransform FindScaleTransform(UIElement target)
    {
        return target.RenderTransform switch
        {
            ScaleTransform scaleTransform => scaleTransform,
            TransformGroup transformGroup => transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault(),
            _ => null,
        };
    }
}
