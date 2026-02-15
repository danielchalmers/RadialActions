using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace RadialActions;

public static class PieVisualBuilder
{
    public readonly record struct CenterElements(
        Grid Target,
        SolidColorBrush FillBrush,
        SolidColorBrush StrokeBrush,
        TextBlock Icon);

    public static void AddSurfaceRing(
        Canvas canvas,
        Point center,
        double outerRadius,
        double innerRadius,
        Color fillColor,
        Color borderColor,
        double strokeThickness,
        bool isHighContrast,
        Effect ambientShadowEffect)
    {
        if (outerRadius <= 0)
        {
            return;
        }

        Geometry ringGeometry;
        if (innerRadius > 0)
        {
            ringGeometry = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new EllipseGeometry(center, outerRadius, outerRadius),
                new EllipseGeometry(center, innerRadius, innerRadius));
        }
        else
        {
            ringGeometry = new EllipseGeometry(center, outerRadius, outerRadius);
        }

        var surfacePath = new Path
        {
            Data = ringGeometry,
            Fill = new SolidColorBrush(fillColor),
            Stroke = new SolidColorBrush(borderColor),
            StrokeThickness = strokeThickness,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
        };

        if (!isHighContrast && ambientShadowEffect != null)
        {
            surfacePath.Effect = ambientShadowEffect.CloneCurrentValue();
        }

        Panel.SetZIndex(surfacePath, 0);
        canvas.Children.Add(surfacePath);
    }

    public static CenterElements CreateCenterElements(
        double innerRadius,
        double hubStrokeThickness,
        Color hubColor,
        Color hubBorderColor,
        Color iconTextColor,
        Style hubEllipseStyle,
        Style hubContainerStyle,
        Style iconTextStyle)
    {
        var centerFillBrush = new SolidColorBrush(hubColor);
        var centerStrokeBrush = new SolidColorBrush(hubBorderColor);

        var centerHole = new Ellipse
        {
            Style = hubEllipseStyle,
            Width = innerRadius * 2,
            Height = innerRadius * 2,
            Fill = centerFillBrush,
            Stroke = centerStrokeBrush,
            StrokeThickness = hubStrokeThickness,
            Cursor = System.Windows.Input.Cursors.Hand,
            SnapsToDevicePixels = true,
        };

        var centerCloseIcon = new TextBlock
        {
            Style = iconTextStyle,
            Text = "\uE8BB",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Foreground = new SolidColorBrush(iconTextColor),
            FontSize = Math.Max(innerRadius * 0.40, 11),
            Margin = new Thickness(0, -1, 0, 0),
            Opacity = 0,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };

        var centerCloseTarget = new Grid
        {
            Style = hubContainerStyle,
            Width = innerRadius * 2,
            Height = innerRadius * 2,
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        centerCloseTarget.Children.Add(centerHole);
        centerCloseTarget.Children.Add(centerCloseIcon);

        return new CenterElements(centerCloseTarget, centerFillBrush, centerStrokeBrush, centerCloseIcon);
    }

    public static StackPanel CreateSliceContentPanel(
        PieAction sliceAction,
        Style iconTextStyle,
        Style labelTextStyle,
        Color iconTextColor,
        Color labelTextColor,
        double iconToLabelSpacing,
        double outerRadius,
        double contentMaxWidthRatio,
        Thickness contentPadding)
    {
        var showIcon = !string.IsNullOrEmpty(sliceAction.Icon);
        if (!showIcon && string.IsNullOrWhiteSpace(sliceAction.Name))
        {
            return null;
        }

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = contentPadding,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
        };

        if (showIcon)
        {
            contentPanel.Children.Add(new TextBlock
            {
                Style = iconTextStyle,
                Text = sliceAction.Icon,
                Foreground = new SolidColorBrush(iconTextColor),
                FontSize = 20,
                Margin = new Thickness(0, 0, 0, iconToLabelSpacing),
            });
        }

        if (!string.IsNullOrWhiteSpace(sliceAction.Name))
        {
            contentPanel.Children.Add(new TextBlock
            {
                Style = labelTextStyle,
                Text = sliceAction.Name,
                Foreground = new SolidColorBrush(labelTextColor),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = Math.Max(56, outerRadius * contentMaxWidthRatio),
            });
        }

        return contentPanel;
    }
}
