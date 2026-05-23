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

    public void AnimateBrushColor(
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

    public void AnimateOpacity(
        UIElement element,
        double toOpacity,
        Duration duration,
        IEasingFunction easingFunction,
        Action onCompleted = null)
    {
        if (IsReducedMotionEnabled())
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = toOpacity;
            onCompleted?.Invoke();
            return;
        }

        var opacityAnimation = new DoubleAnimation
        {
            To = toOpacity,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        if (onCompleted != null)
        {
            opacityAnimation.Completed += (_, _) => onCompleted();
        }

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    public void AnimateClickDown(UIElement target, Duration duration, IEasingFunction easingFunction)
    {
        if (target.RenderTransform is not ScaleTransform scaleTransform)
        {
            scaleTransform = new ScaleTransform(1, 1);
            target.RenderTransform = scaleTransform;
            target.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        if (IsReducedMotionEnabled())
        {
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scaleTransform.ScaleX = 0.95;
            scaleTransform.ScaleY = 0.95;
            return;
        }

        var scaleAnimation = new DoubleAnimation
        {
            To = 0.95,
            Duration = duration,
            EasingFunction = easingFunction,
            FillBehavior = FillBehavior.HoldEnd,
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    public void AnimateClickUp(UIElement target, Duration duration, IEasingFunction easingFunction)
    {
        if (target.RenderTransform is not ScaleTransform scaleTransform)
        {
            return;
        }

        if (IsReducedMotionEnabled())
        {
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scaleTransform.ScaleX = 1;
            scaleTransform.ScaleY = 1;
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

    public static bool IsReducedMotionEnabled()
    {
        return !SystemParameters.ClientAreaAnimation;
    }
}
