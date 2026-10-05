using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace RadialActions;

public static class PieVisualBuilder
{
    private const string CloseGlyph = "\uE8BB";
    private const string ReleaseHintGlyph = "\uE945";

    public readonly record struct CenterElements(
        Grid Target,
        SolidColorBrush FillBrush,
        SolidColorBrush GlyphBrush);

    public readonly record struct SliceContent(
        StackPanel Panel,
        TextBlock Icon,
        TextBlock Label);

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

        UIElement surfaceElement = surfacePath;

        if (!isHighContrast && ambientShadowEffect != null)
        {
            surfacePath.Effect = ambientShadowEffect.CloneCurrentValue();

            // Wrap in a cached container so the expensive shadow blur is baked into a bitmap once instead of being re-evaluated on every animation frame.
            surfaceElement = new Border
            {
                Child = surfacePath,
                IsHitTestVisible = false,
                CacheMode = new BitmapCache(VisualTreeHelper.GetDpi(canvas).PixelsPerDip),
            };
        }

        Panel.SetZIndex(surfaceElement, 0);
        canvas.Children.Add(surfaceElement);
    }

    public static CenterElements CreateCenterElements(
        double innerRadius,
        double hubStrokeThickness,
        Color hubColor,
        Color strokeColor,
        Color glyphColor,
        FontFamily symbolFontFamily,
        Style hubEllipseStyle,
        Style hubContainerStyle,
        Style glyphTextStyle,
        double contentScale)
    {
        var centerFillBrush = new SolidColorBrush(hubColor);
        var centerGlyphBrush = new SolidColorBrush(glyphColor);

        var centerHole = new Ellipse
        {
            Style = hubEllipseStyle,
            Width = innerRadius * 2,
            Height = innerRadius * 2,
            Fill = centerFillBrush,
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = hubStrokeThickness,
            Cursor = System.Windows.Input.Cursors.Hand,
            SnapsToDevicePixels = true,
        };

        // Always shown so the center reads as a close button, not a blank decoration.
        var centerCloseGlyph = new TextBlock
        {
            Style = glyphTextStyle,
            Text = CloseGlyph,
            FontFamily = symbolFontFamily,
            Foreground = centerGlyphBrush,
            IsHitTestVisible = false,
        };
        ScaleFontSize(centerCloseGlyph, contentScale);

        var centerCloseTarget = new Grid
        {
            Style = hubContainerStyle,
            Width = innerRadius * 2,
            Height = innerRadius * 2,
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        centerCloseTarget.Children.Add(centerHole);
        centerCloseTarget.Children.Add(centerCloseGlyph);

        return new CenterElements(centerCloseTarget, centerFillBrush, centerGlyphBrush);
    }

    public static TextBlock CreateSliceDigitHint(
        int digit,
        Style hintTextStyle,
        Brush foreground,
        double contentScale)
    {
        var hint = new TextBlock
        {
            Style = hintTextStyle,
            Text = digit.ToString(),
            Foreground = foreground,
            IsHitTestVisible = false,
        };
        ScaleFontSize(hint, contentScale);
        return hint;
    }

    public static TextBlock CreateSliceReleaseHint(
        Style glyphTextStyle,
        FontFamily symbolFontFamily,
        Brush foreground,
        double contentScale)
    {
        var hint = new TextBlock
        {
            Style = glyphTextStyle,
            Text = ReleaseHintGlyph,
            FontFamily = symbolFontFamily,
            Foreground = foreground,
            Opacity = 0,
            IsHitTestVisible = false,
        };
        ScaleFontSize(hint, contentScale);
        return hint;
    }

    public static Path CreateSelectionArc(
        Geometry arcGeometry,
        double thickness,
        Brush stroke,
        Style selectionArcStyle,
        Transform sliceTransform)
    {
        return new Path
        {
            Style = selectionArcStyle,
            Data = arcGeometry,
            Stroke = stroke,
            StrokeThickness = thickness,
            IsHitTestVisible = false,
            Opacity = 0,

            // Shares the slice's press scale and reorder rotation so the arc moves with its slice.
            RenderTransform = sliceTransform,
        };
    }

    /// <summary>
    /// Builds the icon stacked over the label, or returns an empty <see cref="SliceContent"/> when the action has neither.
    /// </summary>
    public static SliceContent CreateSliceContent(
        PieAction sliceAction,
        Style iconTextStyle,
        Style labelTextStyle,
        Brush iconForeground,
        Brush labelForeground,
        double iconToLabelSpacing,
        Thickness contentPadding,
        double contentScale)
    {
        var showIcon = !string.IsNullOrEmpty(sliceAction.Icon);
        var showLabel = !string.IsNullOrWhiteSpace(sliceAction.Name);
        if (!showIcon && !showLabel)
        {
            return default;
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

        TextBlock icon = null;
        if (showIcon)
        {
            icon = new TextBlock
            {
                Style = iconTextStyle,
                Text = sliceAction.Icon,
                Foreground = iconForeground,
                Margin = new Thickness(0, 0, 0, showLabel ? iconToLabelSpacing : 0),
            };
            ScaleFontSize(icon, contentScale);
            contentPanel.Children.Add(icon);
        }

        TextBlock label = null;
        if (showLabel)
        {
            label = new TextBlock
            {
                Style = labelTextStyle,
                Text = sliceAction.Name,
                Foreground = labelForeground,
            };
            ScaleFontSize(label, contentScale);
            contentPanel.Children.Add(label);
        }

        return new SliceContent(contentPanel, icon, label);
    }

    /// <summary>
    /// Removes the label so the slice shows its icon alone, for slices too narrow for even a trimmed name.
    /// </summary>
    public static void DropLabel(SliceContent content)
    {
        if (content.Label == null || content.Icon == null)
        {
            return;
        }

        content.Panel.Children.Remove(content.Label);
        content.Icon.Margin = new Thickness(0);
    }

    private static void ScaleFontSize(TextBlock textBlock, double contentScale)
    {
        // The style carries the base size for a default menu; larger menus scale it here rather than overriding it with a literal.
        if (contentScale != 1)
        {
            textBlock.FontSize *= contentScale;
        }
    }
}
