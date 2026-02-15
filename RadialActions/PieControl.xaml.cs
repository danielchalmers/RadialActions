using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace RadialActions;

/// <summary>
/// A radial pie menu control that displays clickable slices.
/// </summary>
public partial class PieControl : UserControl
{
    private const double DefaultCenterHoleRatio = 0.25;
    private const double HoverAnimationSeconds = 0.12;
    private const double PressAnimationSeconds = 0.08;
    private const double ReleaseAnimationSeconds = 0.10;
    private const int DefaultSliceZIndex = 5;
    private const int HoveredSliceZIndex = 9;

    public PieControl()
    {
        InitializeComponent();
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        PieCanvas.UseLayoutRounding = true;
        PieCanvas.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (s, e) => CreatePieMenu();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CreatePieMenu();
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    public static readonly DependencyProperty SlicesProperty =
        DependencyProperty.Register(
            nameof(Slices),
            typeof(ObservableCollection<PieAction>),
            typeof(PieControl),
            new PropertyMetadata(null, OnSlicesPropertyChanged));

    public ObservableCollection<PieAction> Slices
    {
        get => (ObservableCollection<PieAction>)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    private static void OnSlicesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PieControl control)
        {
            if (e.OldValue is ObservableCollection<PieAction> oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnSlicesCollectionChanged;
                foreach (var item in oldCollection)
                {
                    item.PropertyChanged -= control.OnSlicePropertyChanged;
                }
            }

            if (e.NewValue is ObservableCollection<PieAction> newCollection)
            {
                newCollection.CollectionChanged += control.OnSlicesCollectionChanged;
                foreach (var item in newCollection)
                {
                    item.PropertyChanged += control.OnSlicePropertyChanged;
                }
            }

            control.CreatePieMenu();
        }
    }

    private void OnSlicesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (PieAction item in e.OldItems)
            {
                item.PropertyChanged -= OnSlicePropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (PieAction item in e.NewItems)
            {
                item.PropertyChanged += OnSlicePropertyChanged;
            }
        }

        CreatePieMenu();
    }

    private void OnSlicePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        CreatePieMenu();
    }

    private void CreatePieMenu()
    {
        PieCanvas.Children.Clear();

        if (Slices == null || Slices.Count == 0 || ActualWidth == 0 || ActualHeight == 0)
            return;

        var canvasSize = Math.Min(ActualWidth, ActualHeight);
        var canvasRadius = canvasSize / 2;
        var center = new Point(canvasRadius, canvasRadius);
        var palette = BuildPalette();
        var showCenterHole = true;
        var showIcons = true;
        var showLabels = true;

        PieCanvas.Width = canvasSize;
        PieCanvas.Height = canvasSize;

        var innerRadius = showCenterHole ? canvasRadius * DefaultCenterHoleRatio : 0;
        var angleStep = 360.0 / Slices.Count;

        DrawMenuBackdrop(center, canvasRadius, palette);

        // Draw center hole background if enabled
        if (showCenterHole && innerRadius > 0)
        {
            var centerHole = new Ellipse
            {
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Fill = new SolidColorBrush(palette.CenterFill),
                Stroke = new SolidColorBrush(palette.CenterStroke),
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(centerHole, center.X - innerRadius);
            Canvas.SetTop(centerHole, center.Y - innerRadius);
            Panel.SetZIndex(centerHole, 20);
            PieCanvas.Children.Add(centerHole);
        }

        for (var i = 0; i < Slices.Count; i++)
        {
            var sliceAction = Slices[i];
            var startAngle = (i * angleStep) - 90; // Start from top
            var endAngle = startAngle + angleStep;

            // Create the pie slice
            var slice = CreateSlice(center, canvasRadius, innerRadius, startAngle, endAngle);

            // Use a SolidColorBrush for animation
            var fillBrush = new SolidColorBrush(palette.SliceFill);
            var borderBrush = new SolidColorBrush(palette.SliceBorder);
            var iconBrush = new SolidColorBrush(palette.IconColor);
            var labelBrush = new SolidColorBrush(palette.TextColor);
            slice.Fill = fillBrush;
            slice.Stroke = borderBrush;
            slice.StrokeThickness = 1.5;
            slice.Cursor = System.Windows.Input.Cursors.Hand;
            slice.RenderTransformOrigin = new Point(0.5, 0.5);
            var sliceScaleTransform = new ScaleTransform(1, 1);
            slice.RenderTransform = sliceScaleTransform;

            var isMouseDown = false;

            // Add mouse-down animation
            slice.MouseLeftButtonDown += (s, e) =>
            {
                isMouseDown = true;
                slice.CaptureMouse();
                AnimateScale(sliceScaleTransform, 0.965, PressAnimationSeconds);
                ApplyInteractionState(fillBrush, borderBrush, iconBrush, labelBrush, palette, isHovered: true, isPressed: true);
                e.Handled = true;
            };

            // Add mouse-up animation
            slice.MouseLeftButtonUp += (s, e) =>
            {
                if (isMouseDown)
                {
                    isMouseDown = false;
                    slice.ReleaseMouseCapture();
                    var keepHoverState = slice.IsMouseOver;
                    ApplyInteractionState(fillBrush, borderBrush, iconBrush, labelBrush, palette, isHovered: keepHoverState, isPressed: false);
                    AnimateScale(sliceScaleTransform, 1.0, ReleaseAnimationSeconds);
                    SliceClicked?.Invoke(this, new SliceClickEventArgs(sliceAction));
                    e.Handled = true;
                }
            };

            var contextMenu = new ContextMenu();
            var editMenuItem = new MenuItem { Header = "Edit..." };
            editMenuItem.Click += (s, e) => SliceEditRequested?.Invoke(this, new SliceClickEventArgs(sliceAction));
            contextMenu.Items.Add(editMenuItem);
            slice.ContextMenu = contextMenu;

            // Calculate the center position of the slice for icon/text
            var textRadius = innerRadius > 0
                ? (canvasRadius + innerRadius) / 2
                : canvasRadius * 0.6;
            var textPosition = GetTextPosition(center, textRadius, startAngle, endAngle);

            var showIcon = showIcons && !string.IsNullOrEmpty(sliceAction.Icon);
            var showLabel = showLabels;

            if (showIcon || showLabel)
            {
                var contentPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                if (showIcon)
                {
                    var iconText = new TextBlock
                    {
                        Text = sliceAction.Icon,
                        Foreground = iconBrush,
                        FontSize = showLabel ? 20 : 28,
                        TextAlignment = TextAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = showLabel ? new Thickness(0, 0, 0, 4) : new Thickness(0),
                    };

                    contentPanel.Children.Add(iconText);
                }

                if (showLabel)
                {
                    var text = new TextBlock
                    {
                        Text = sliceAction.Name,
                        Foreground = labelBrush,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        TextAlignment = TextAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = canvasRadius * 0.4,
                    };

                    contentPanel.Children.Add(text);
                }

                contentPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var contentSize = contentPanel.DesiredSize;

                var rawLeft = textPosition.X - (contentSize.Width / 2);
                var rawTop = textPosition.Y - (contentSize.Height / 2);
                Canvas.SetLeft(contentPanel, SnapToDevicePixels(rawLeft, true));
                Canvas.SetTop(contentPanel, SnapToDevicePixels(rawTop, false));

                PieCanvas.Children.Add(contentPanel);
                Panel.SetZIndex(contentPanel, 10);
            }

            // Add hover effects once visual references exist.
            slice.MouseEnter += (s, e) =>
            {
                Panel.SetZIndex(slice, HoveredSliceZIndex);
                ApplyInteractionState(fillBrush, borderBrush, iconBrush, labelBrush, palette, isHovered: true, isPressed: false);
            };

            slice.MouseLeave += (s, e) =>
            {
                Panel.SetZIndex(slice, DefaultSliceZIndex);
                ApplyInteractionState(fillBrush, borderBrush, iconBrush, labelBrush, palette, isHovered: false, isPressed: false);

                if (isMouseDown)
                {
                    isMouseDown = false;
                    slice.ReleaseMouseCapture();
                    AnimateScale(sliceScaleTransform, 1.0, ReleaseAnimationSeconds);
                }
            };

            ApplyInteractionState(fillBrush, borderBrush, iconBrush, labelBrush, palette, isHovered: false, isPressed: false);
            PieCanvas.Children.Add(slice);
            Panel.SetZIndex(slice, DefaultSliceZIndex);
        }
    }

    private void DrawMenuBackdrop(Point center, double radius, PiePalette palette)
    {
        var diameter = radius * 2;
        var backdropBrush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            RadiusX = 0.56,
            RadiusY = 0.56,
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.45)
        };
        backdropBrush.GradientStops.Add(new GradientStop(palette.MenuBackdropInner, 0.0));
        backdropBrush.GradientStops.Add(new GradientStop(palette.MenuBackdropOuter, 1.0));

        var backdrop = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = backdropBrush,
            Stroke = new SolidColorBrush(palette.MenuBackdropBorder),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(backdrop, center.X - radius);
        Canvas.SetTop(backdrop, center.Y - radius);
        Panel.SetZIndex(backdrop, 0);
        PieCanvas.Children.Add(backdrop);
    }

    private void ApplyInteractionState(
        SolidColorBrush fillBrush,
        SolidColorBrush borderBrush,
        SolidColorBrush iconBrush,
        SolidColorBrush labelBrush,
        PiePalette palette,
        bool isHovered,
        bool isPressed)
    {
        var targetFill = isPressed
            ? palette.SliceFillPressed
            : isHovered ? palette.SliceFillHover : palette.SliceFill;

        var targetBorder = isHovered || isPressed
            ? palette.SliceBorderHover
            : palette.SliceBorder;

        var targetIcon = isHovered || isPressed
            ? palette.IconHoverColor
            : palette.IconColor;

        var targetLabel = isHovered || isPressed
            ? palette.TextHoverColor
            : palette.TextColor;

        AnimateColor(fillBrush, targetFill, HoverAnimationSeconds);
        AnimateColor(borderBrush, targetBorder, HoverAnimationSeconds);
        AnimateColor(iconBrush, targetIcon, HoverAnimationSeconds);
        AnimateColor(labelBrush, targetLabel, HoverAnimationSeconds);
    }

    private static PiePalette BuildPalette()
    {
        if (SystemParameters.HighContrast)
        {
            var window = SystemColors.WindowColor;
            var windowText = SystemColors.WindowTextColor;
            var highlight = SystemColors.HighlightColor;
            var highlightText = SystemColors.HighlightTextColor;

            return new(
                MenuBackdropInner: window,
                MenuBackdropOuter: window,
                MenuBackdropBorder: windowText,
                SliceFill: window,
                SliceFillHover: highlight,
                SliceFillPressed: BlendColor(highlight, windowText, 0.15),
                SliceBorder: windowText,
                SliceBorderHover: highlightText,
                CenterFill: window,
                CenterStroke: windowText,
                IconColor: windowText,
                IconHoverColor: highlightText,
                TextColor: windowText,
                TextHoverColor: highlightText);
        }

        var windowBackground = GetThemeColor(
            Color.FromRgb(32, 32, 36),
            "WindowBackgroundBrush",
            "SolidBackgroundFillColorBaseBrush",
            "SolidBackgroundFillColorBase",
            SystemColors.WindowBrushKey);

        var layerColor = GetThemeColor(
            BlendColor(windowBackground, Colors.White, 0.08),
            "LayerFillColorDefaultBrush",
            "CardBackgroundFillColorDefaultBrush",
            "ControlFillColorDefaultBrush");

        var layerAltColor = GetThemeColor(
            layerColor,
            "LayerFillColorAltBrush",
            "CardBackgroundFillColorSecondaryBrush",
            "ControlFillColorSecondaryBrush");

        var strokeColor = GetThemeColor(
            Color.FromRgb(122, 122, 122),
            "CardStrokeColorDefaultBrush",
            "ControlStrokeColorDefaultBrush",
            "SurfaceStrokeColorDefaultBrush");

        var textPrimary = GetThemeColor(
            Color.FromRgb(240, 240, 240),
            "TextFillColorPrimaryBrush",
            SystemColors.ControlTextBrushKey);

        var accentResourceColor = GetThemeColor(
            SystemParameters.WindowGlassColor,
            "AccentTextFillColorPrimaryBrush",
            "AccentFillColorDefaultBrush");

        var isDark = IsDarkColor(windowBackground);
        var accent = NormalizeAccentColor(accentResourceColor, isDark);

        var menuBackdropInner = WithAlpha(BlendColor(windowBackground, layerColor, 0.35), isDark ? (byte)188 : (byte)176);
        var menuBackdropOuter = WithAlpha(BlendColor(windowBackground, layerAltColor, 0.10), isDark ? (byte)112 : (byte)96);
        var menuBackdropBorder = WithAlpha(strokeColor, isDark ? (byte)132 : (byte)92);

        var sliceFill = WithAlpha(layerColor, isDark ? (byte)222 : (byte)232);
        var sliceFillHover = BlendColor(sliceFill, accent, isDark ? 0.16 : 0.10);
        var sliceFillPressed = BlendColor(sliceFill, accent, isDark ? 0.24 : 0.16);

        var sliceBorder = WithAlpha(strokeColor, isDark ? (byte)160 : (byte)120);
        var sliceBorderHover = WithAlpha(accent, isDark ? (byte)210 : (byte)182);

        var centerFill = WithAlpha(BlendColor(windowBackground, layerAltColor, 0.45), isDark ? (byte)236 : (byte)242);
        var centerStroke = WithAlpha(strokeColor, isDark ? (byte)176 : (byte)132);

        var iconColor = EnsureContrast(accent, sliceFill, 3.0, shouldLighten: isDark);
        var iconHover = EnsureContrast(
            BlendColor(accent, textPrimary, isDark ? 0.22 : 0.18),
            sliceFillHover,
            3.5,
            shouldLighten: isDark);

        var textColor = EnsureContrast(
            BlendColor(accent, textPrimary, 0.52),
            sliceFill,
            3.2,
            shouldLighten: isDark);

        var textHover = EnsureContrast(
            BlendColor(accent, textPrimary, 0.36),
            sliceFillHover,
            3.6,
            shouldLighten: isDark);

        return new(
            MenuBackdropInner: menuBackdropInner,
            MenuBackdropOuter: menuBackdropOuter,
            MenuBackdropBorder: menuBackdropBorder,
            SliceFill: sliceFill,
            SliceFillHover: sliceFillHover,
            SliceFillPressed: sliceFillPressed,
            SliceBorder: sliceBorder,
            SliceBorderHover: sliceBorderHover,
            CenterFill: centerFill,
            CenterStroke: centerStroke,
            IconColor: iconColor,
            IconHoverColor: iconHover,
            TextColor: textColor,
            TextHoverColor: textHover);
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var a = (byte)Math.Round((from.A * (1 - amount)) + (to.A * amount));
        var r = (byte)Math.Round((from.R * (1 - amount)) + (to.R * amount));
        var g = (byte)Math.Round((from.G * (1 - amount)) + (to.G * amount));
        var b = (byte)Math.Round((from.B * (1 - amount)) + (to.B * amount));
        return Color.FromArgb(a, r, g, b);
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static bool IsDarkColor(Color color)
    {
        return GetRelativeLuminance(color) < 0.45;
    }

    private static Color GetThemeColor(Color fallback, params object[] keys)
    {
        foreach (var key in keys)
        {
            if (TryGetThemeColor(key, out var color))
            {
                return color;
            }
        }

        return fallback;
    }

    private static bool TryGetThemeColor(object key, out Color color)
    {
        color = default;
        var resource = Application.Current?.TryFindResource(key);
        switch (resource)
        {
            case SolidColorBrush brush:
                color = brush.Color;
                return true;
            case Color directColor:
                color = directColor;
                return true;
            default:
                return false;
        }
    }

    private double SnapToDevicePixels(double value, bool useXScale)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var scale = useXScale ? dpi.DpiScaleX : dpi.DpiScaleY;
        return Math.Round(value * scale) / scale;
    }

    private static Color NormalizeAccentColor(Color accentColor, bool isDark)
    {
        var accent = accentColor.A == 0
            ? Color.FromRgb(0, 120, 212)
            : Color.FromRgb(accentColor.R, accentColor.G, accentColor.B);

        var luminance = GetRelativeLuminance(accent);
        if (isDark && luminance < 0.35)
        {
            accent = BlendColor(accent, Colors.White, 0.25);
        }
        else if (!isDark && luminance > 0.68)
        {
            accent = BlendColor(accent, Colors.Black, 0.34);
        }

        return Color.FromRgb(accent.R, accent.G, accent.B);
    }

    private static Color EnsureContrast(Color foreground, Color background, double minContrast, bool shouldLighten)
    {
        var adjusted = Color.FromRgb(foreground.R, foreground.G, foreground.B);
        for (var i = 0; i < 8 && GetContrastRatio(adjusted, background) < minContrast; i++)
        {
            adjusted = BlendColor(adjusted, shouldLighten ? Colors.White : Colors.Black, 0.13);
        }

        return adjusted;
    }

    private static double GetContrastRatio(Color first, Color second)
    {
        var luminanceA = GetRelativeLuminance(first);
        var luminanceB = GetRelativeLuminance(second);
        var lighter = Math.Max(luminanceA, luminanceB);
        var darker = Math.Min(luminanceA, luminanceB);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(Color color)
    {
        var r = GetLinearColorComponent(color.R / 255.0);
        var g = GetLinearColorComponent(color.G / 255.0);
        var b = GetLinearColorComponent(color.B / 255.0);
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static double GetLinearColorComponent(double component)
    {
        return component <= 0.03928
            ? component / 12.92
            : Math.Pow((component + 0.055) / 1.055, 2.4);
    }

    private void OnSystemParametersChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.WindowGlassColor)
            or nameof(SystemParameters.HighContrast))
        {
            Dispatcher.InvokeAsync(CreatePieMenu);
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(CreatePieMenu);
    }

    private static void AnimateColor(SolidColorBrush brush, Color toColor, double durationInSeconds)
    {
        var colorAnimation = new ColorAnimation
        {
            To = toColor,
            Duration = TimeSpan.FromSeconds(durationInSeconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void AnimateScale(ScaleTransform scaleTransform, double to, double durationInSeconds)
    {
        var scaleAnimation = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromSeconds(durationInSeconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private Point GetTextPosition(Point center, double radius, double startAngle, double endAngle)
    {
        var midAngle = (startAngle + endAngle) / 2;

        var x = center.X + (radius * Math.Cos(midAngle * Math.PI / 180));
        var y = center.Y + (radius * Math.Sin(midAngle * Math.PI / 180));

        return new(x, y);
    }

    private Path CreateSlice(Point center, double outerRadius, double innerRadius, double startAngle, double endAngle)
    {
        var startPointOuter = GetPointOnCircle(center, outerRadius, startAngle);
        var endPointOuter = GetPointOnCircle(center, outerRadius, endAngle);
        var startPointInner = GetPointOnCircle(center, innerRadius, startAngle);
        var endPointInner = GetPointOnCircle(center, innerRadius, endAngle);

        var figure = new PathFigure { StartPoint = startPointInner };

        // Line from inner start to outer start
        figure.Segments.Add(new LineSegment(startPointOuter, true));

        // Arc along outer edge
        figure.Segments.Add(new ArcSegment(
            endPointOuter,
            new Size(outerRadius, outerRadius),
            0,
            endAngle - startAngle > 180,
            SweepDirection.Clockwise,
            true));

        // Line from outer end to inner end
        figure.Segments.Add(new LineSegment(endPointInner, true));

        // Arc along inner edge (back to start)
        if (innerRadius > 0)
        {
            figure.Segments.Add(new ArcSegment(
                startPointInner,
                new Size(innerRadius, innerRadius),
                0,
                endAngle - startAngle > 180,
                SweepDirection.Counterclockwise,
                true));
        }
        else
        {
            figure.Segments.Add(new LineSegment(center, true));
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Path { Data = geometry };
    }

    private Point GetPointOnCircle(Point center, double radius, double angleInDegrees)
    {
        var angleInRadians = angleInDegrees * Math.PI / 180;
        var x = center.X + (radius * Math.Cos(angleInRadians));
        var y = center.Y + (radius * Math.Sin(angleInRadians));
        return new Point(x, y);
    }

    /// <summary>
    /// Occurs when a slice is clicked.
    /// </summary>
    public event EventHandler<SliceClickEventArgs> SliceClicked;

    /// <summary>
    /// Occurs when a slice edit is requested from the context menu.
    /// </summary>
    public event EventHandler<SliceClickEventArgs> SliceEditRequested;

    private sealed record PiePalette(
        Color MenuBackdropInner,
        Color MenuBackdropOuter,
        Color MenuBackdropBorder,
        Color SliceFill,
        Color SliceFillHover,
        Color SliceFillPressed,
        Color SliceBorder,
        Color SliceBorderHover,
        Color CenterFill,
        Color CenterStroke,
        Color IconColor,
        Color IconHoverColor,
        Color TextColor,
        Color TextHoverColor);
}

/// <summary>
/// Event arguments for slice click events.
/// </summary>
public class SliceClickEventArgs : EventArgs
{
    public PieAction Slice { get; }

    public SliceClickEventArgs(PieAction action)
    {
        Slice = action;
    }
}
