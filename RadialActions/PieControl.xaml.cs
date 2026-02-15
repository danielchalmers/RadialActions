using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace RadialActions;

/// <summary>
/// A radial pie menu control that displays clickable slices.
/// </summary>
public partial class PieControl : UserControl
{
    private const double DefaultCenterHoleRatio = 0.25;

    public PieControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => CreatePieMenu();
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
        if (d is not PieControl control)
        {
            return;
        }

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

        if (Slices == null || Slices.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var canvasSize = SnapToDevicePixel(Math.Min(ActualWidth, ActualHeight), isXAxis: true);
        if (canvasSize <= 0)
        {
            return;
        }

        var canvasRadius = canvasSize / 2;
        var center = new Point(canvasRadius, canvasRadius);

        PieCanvas.Width = canvasSize;
        PieCanvas.Height = canvasSize;

        var isHighContrast = SystemParameters.HighContrast;
        var showCenterHole = true;
        var showIcons = true;
        var showLabels = true;

        var sliceStrokeThickness = Math.Max(1, GetDoubleResource("RadialMenuSliceStrokeThickness", 1.5));
        var hubStrokeThickness = Math.Max(1, GetDoubleResource("RadialMenuHubStrokeThickness", 1.5));
        var iconToLabelSpacing = Math.Max(0, GetDoubleResource("RadialMenuIconToLabelSpacing", 3));
        var contentMaxWidthRatio = Math.Clamp(GetDoubleResource("RadialMenuSliceContentMaxWidthRatio", 0.38), 0.2, 0.8);

        var hoverDuration = GetDurationResource("RadialMenuHoverDuration", new Duration(TimeSpan.FromMilliseconds(100)));
        var pressDuration = GetDurationResource("RadialMenuPressDuration", new Duration(TimeSpan.FromMilliseconds(75)));
        var standardEasing = GetEasingResource(
            "RadialMenuEaseStandard",
            new QuadraticEase { EasingMode = EasingMode.EaseOut });

        var accentColor = GetBrushResource("RadialMenuSliceAccentBrush", SystemParameters.WindowGlassColor).Color;
        var surfaceColor = GetBrushResource("RadialMenuSurfaceBrush", SystemColors.ControlColor).Color;
        var surfaceBorderColor = GetBrushResource("RadialMenuSurfaceBorderBrush", SystemColors.ControlDarkColor).Color;
        var sliceColor = GetBrushResource("RadialMenuSliceFillBrush", SystemColors.ControlColor).Color;
        var hoverColor = GetBrushResource("RadialMenuSliceHoverBrush", SystemColors.ControlLightColor).Color;
        var pressedColor = GetBrushResource("RadialMenuSlicePressedBrush", SystemColors.ControlDarkColor).Color;
        var borderColor = GetBrushResource("RadialMenuSliceBorderBrush", SystemColors.ControlDarkColor).Color;
        var hubColor = GetBrushResource("RadialMenuHubFillBrush", SystemColors.ControlColor).Color;
        var hubHoverColor = GetBrushResource("RadialMenuHubHoverBrush", SystemColors.ControlLightColor).Color;
        var hubBorderColor = GetBrushResource("RadialMenuHubBorderBrush", SystemColors.ControlDarkColor).Color;
        var iconTextColor = GetBrushResource("RadialMenuSliceAccentBrush", SystemColors.WindowTextColor).Color;
        var labelTextColor = GetBrushResource("RadialMenuTextBrush", SystemColors.WindowTextColor).Color;

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

        var borderHoverColor = BlendColor(borderColor, accentColor, 0.40);
        var centerHoverBorderColor = BlendColor(hubBorderColor, accentColor, 0.45);

        var innerRadius = showCenterHole ? canvasRadius * DefaultCenterHoleRatio : 0;
        var outerRadius = Math.Max(0, canvasRadius - (sliceStrokeThickness / 2));

        AddSurfaceRing(center, outerRadius, innerRadius, surfaceColor, surfaceBorderColor, sliceStrokeThickness, isHighContrast);

        var angleStep = 360.0 / Slices.Count;

        if (showCenterHole && innerRadius > 0)
        {
            var centerFillBrush = new SolidColorBrush(hubColor);
            var centerStrokeBrush = new SolidColorBrush(hubBorderColor);
            var hubStyle = TryFindResource("PieHubEllipseStyle") as Style;

            var centerHole = new Ellipse
            {
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Fill = centerFillBrush,
                Stroke = centerStrokeBrush,
                StrokeThickness = hubStrokeThickness,
                Cursor = System.Windows.Input.Cursors.Hand,
                SnapsToDevicePixels = true,
            };

            if (hubStyle != null)
            {
                centerHole.Style = hubStyle;
                centerHole.Fill = centerFillBrush;
                centerHole.Stroke = centerStrokeBrush;
                centerHole.StrokeThickness = hubStrokeThickness;
            }

            var iconStyle = TryFindResource("PieIconTextStyle") as Style;
            var centerCloseIcon = new TextBlock
            {
                Text = "\uE8BB",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Foreground = new SolidColorBrush(iconTextColor),
                FontSize = Math.Max(innerRadius * 0.40, 11),
                Margin = new Thickness(0, -1, 0, 0),
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            if (iconStyle != null)
            {
                centerCloseIcon.Style = iconStyle;
                centerCloseIcon.FontFamily = new FontFamily("Segoe MDL2 Assets");
                centerCloseIcon.Foreground = new SolidColorBrush(iconTextColor);
                centerCloseIcon.FontSize = Math.Max(innerRadius * 0.40, 11);
                centerCloseIcon.Margin = new Thickness(0, -1, 0, 0);
                centerCloseIcon.Opacity = 0;
                centerCloseIcon.Visibility = Visibility.Collapsed;
            }

            var centerCloseTarget = new Grid
            {
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            if (TryFindResource("PieHubContainerStyle") is Style hubContainerStyle)
            {
                centerCloseTarget.Style = hubContainerStyle;
            }

            centerCloseTarget.Children.Add(centerHole);
            centerCloseTarget.Children.Add(centerCloseIcon);

            var isCenterMouseDown = false;

            centerCloseTarget.MouseEnter += (_, _) =>
            {
                AnimateSliceColor(centerFillBrush, hubHoverColor, hoverDuration, standardEasing);
                AnimateSliceColor(centerStrokeBrush, centerHoverBorderColor, hoverDuration, standardEasing);

                centerCloseIcon.Visibility = Visibility.Visible;
                AnimateOpacity(centerCloseIcon, 1, hoverDuration, standardEasing);
            };

            centerCloseTarget.MouseLeave += (_, _) =>
            {
                AnimateSliceColor(centerFillBrush, hubColor, hoverDuration, standardEasing);
                AnimateSliceColor(centerStrokeBrush, hubBorderColor, hoverDuration, standardEasing);

                AnimateOpacity(centerCloseIcon, 0, hoverDuration, standardEasing, () =>
                {
                    if (!centerCloseTarget.IsMouseOver)
                    {
                        centerCloseIcon.Visibility = Visibility.Collapsed;
                    }
                });

                if (!isCenterMouseDown)
                {
                    return;
                }

                isCenterMouseDown = false;
                AnimateSliceClickUp(centerCloseTarget, pressDuration, standardEasing);
            };

            centerCloseTarget.MouseLeftButtonDown += (_, e) =>
            {
                isCenterMouseDown = true;
                AnimateSliceClickDown(centerCloseTarget, pressDuration, standardEasing);
                e.Handled = true;
            };

            centerCloseTarget.MouseLeftButtonUp += (_, e) =>
            {
                if (!isCenterMouseDown)
                {
                    return;
                }

                isCenterMouseDown = false;
                AnimateSliceClickUp(centerCloseTarget, pressDuration, standardEasing);
                CenterClicked?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            };

            Canvas.SetLeft(centerCloseTarget, SnapToDevicePixel(center.X - innerRadius, isXAxis: true));
            Canvas.SetTop(centerCloseTarget, SnapToDevicePixel(center.Y - innerRadius, isXAxis: false));
            Panel.SetZIndex(centerCloseTarget, 20);
            PieCanvas.Children.Add(centerCloseTarget);
        }

        for (var i = 0; i < Slices.Count; i++)
        {
            var sliceAction = Slices[i];
            var startAngle = (i * angleStep) - 90;
            var endAngle = startAngle + angleStep;

            var slice = CreateSlice(center, outerRadius, innerRadius, startAngle, endAngle);
            if (TryFindResource("PieSlicePathStyle") is Style pathStyle)
            {
                slice.Style = pathStyle;
            }

            var fillBrush = new SolidColorBrush(sliceColor);
            var strokeBrush = new SolidColorBrush(borderColor);

            slice.Fill = fillBrush;
            slice.Stroke = strokeBrush;
            slice.StrokeThickness = sliceStrokeThickness;
            slice.Cursor = System.Windows.Input.Cursors.Hand;
            slice.SnapsToDevicePixels = true;

            var isMouseDown = false;

            slice.MouseLeftButtonDown += (_, e) =>
            {
                isMouseDown = true;
                AnimateSliceColor(fillBrush, pressedColor, pressDuration, standardEasing);
                AnimateSliceColor(strokeBrush, borderHoverColor, pressDuration, standardEasing);
                AnimateSliceClickDown(slice, pressDuration, standardEasing);
                e.Handled = true;
            };

            slice.MouseLeftButtonUp += (_, e) =>
            {
                if (!isMouseDown)
                {
                    return;
                }

                isMouseDown = false;
                AnimateSliceClickUp(slice, pressDuration, standardEasing);
                AnimateSliceColor(fillBrush, slice.IsMouseOver ? hoverColor : sliceColor, hoverDuration, standardEasing);
                AnimateSliceColor(strokeBrush, slice.IsMouseOver ? borderHoverColor : borderColor, hoverDuration, standardEasing);
                SliceClicked?.Invoke(this, new SliceClickEventArgs(sliceAction));
                e.Handled = true;
            };

            slice.MouseEnter += (_, _) =>
            {
                AnimateSliceColor(fillBrush, hoverColor, hoverDuration, standardEasing);
                AnimateSliceColor(strokeBrush, borderHoverColor, hoverDuration, standardEasing);
            };

            slice.MouseLeave += (_, _) =>
            {
                AnimateSliceColor(fillBrush, sliceColor, hoverDuration, standardEasing);
                AnimateSliceColor(strokeBrush, borderColor, hoverDuration, standardEasing);

                if (!isMouseDown)
                {
                    return;
                }

                isMouseDown = false;
                AnimateSliceClickUp(slice, pressDuration, standardEasing);
            };

            var contextMenu = new ContextMenu();
            var editMenuItem = new MenuItem { Header = "Edit..." };
            editMenuItem.Click += (_, _) => SliceEditRequested?.Invoke(this, new SliceClickEventArgs(sliceAction));
            contextMenu.Items.Add(editMenuItem);
            slice.ContextMenu = contextMenu;

            Panel.SetZIndex(slice, 10);
            PieCanvas.Children.Add(slice);

            var textRadius = innerRadius > 0 ? (outerRadius + innerRadius) / 2 : outerRadius * 0.6;
            var textPosition = GetTextPosition(center, textRadius, startAngle, endAngle);

            var showIcon = showIcons && !string.IsNullOrEmpty(sliceAction.Icon);
            var showLabel = showLabels;

            if (!showIcon && !showLabel)
            {
                continue;
            }

            var contentPadding = TryFindResource("RadialMenuSliceContentPadding") is Thickness padding
                ? padding
                : new Thickness(2);

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
                var iconText = new TextBlock
                {
                    Text = sliceAction.Icon,
                    Foreground = new SolidColorBrush(iconTextColor),
                    FontSize = showLabel ? 20 : 27,
                    Margin = showLabel ? new Thickness(0, 0, 0, iconToLabelSpacing) : new Thickness(0),
                };

                if (TryFindResource("PieIconTextStyle") is Style iconStyle)
                {
                    iconText.Style = iconStyle;
                    iconText.Foreground = new SolidColorBrush(iconTextColor);
                    iconText.FontSize = showLabel ? 20 : 27;
                    iconText.Margin = showLabel ? new Thickness(0, 0, 0, iconToLabelSpacing) : new Thickness(0);
                }

                contentPanel.Children.Add(iconText);
            }

            if (showLabel)
            {
                var text = new TextBlock
                {
                    Text = sliceAction.Name,
                    Foreground = new SolidColorBrush(labelTextColor),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = Math.Max(56, outerRadius * contentMaxWidthRatio),
                };

                if (TryFindResource("PieLabelTextStyle") is Style labelStyle)
                {
                    text.Style = labelStyle;
                    text.Foreground = new SolidColorBrush(labelTextColor);
                    text.MaxWidth = Math.Max(56, outerRadius * contentMaxWidthRatio);
                }

                contentPanel.Children.Add(text);
            }

            contentPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var contentSize = contentPanel.DesiredSize;

            Canvas.SetLeft(contentPanel, SnapToDevicePixel(textPosition.X - (contentSize.Width / 2), isXAxis: true));
            Canvas.SetTop(contentPanel, SnapToDevicePixel(textPosition.Y - (contentSize.Height / 2), isXAxis: false));

            Panel.SetZIndex(contentPanel, 15);
            PieCanvas.Children.Add(contentPanel);
        }
    }

    private void AddSurfaceRing(
        Point center,
        double outerRadius,
        double innerRadius,
        Color fillColor,
        Color borderColor,
        double strokeThickness,
        bool isHighContrast)
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

        if (!isHighContrast && GetThemedResource("RadialMenuAmbientShadowEffect") is Effect effect)
        {
            surfacePath.Effect = effect.CloneCurrentValue();
        }

        Panel.SetZIndex(surfacePath, 0);
        PieCanvas.Children.Add(surfacePath);
    }

    private object? GetThemedResource(string resourceKey)
    {
        if (SystemParameters.HighContrast)
        {
            var highContrastKey = $"{resourceKey}.HighContrast";
            if (TryFindResource(highContrastKey) is { } highContrastValue)
            {
                return highContrastValue;
            }
        }

        var themedKey = IsAppDarkModeEnabled() ? $"{resourceKey}.Dark" : $"{resourceKey}.Light";
        if (TryFindResource(themedKey) is { } themedValue)
        {
            return themedValue;
        }

        return TryFindResource(resourceKey);
    }

    private SolidColorBrush GetBrushResource(string resourceKey, Color fallbackColor)
    {
        var value = GetThemedResource(resourceKey);

        if (value is SolidColorBrush solidColorBrush)
        {
            return solidColorBrush;
        }

        if (value is Color color)
        {
            return new SolidColorBrush(color);
        }

        if (value is Brush brush && brush is SolidColorBrush solid)
        {
            return solid;
        }

        return new SolidColorBrush(fallbackColor);
    }

    private double GetDoubleResource(string resourceKey, double fallbackValue)
    {
        var value = GetThemedResource(resourceKey);

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

    private Duration GetDurationResource(string resourceKey, Duration fallbackValue)
    {
        var value = GetThemedResource(resourceKey);

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

    private IEasingFunction GetEasingResource(string resourceKey, IEasingFunction fallbackValue)
    {
        var value = GetThemedResource(resourceKey);
        return value as IEasingFunction ?? fallbackValue;
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

    private void AnimateSliceColor(
        SolidColorBrush brush,
        Color toColor,
        Duration duration,
        IEasingFunction easingFunction)
    {
        var colorAnimation = new ColorAnimation
        {
            To = toColor,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateOpacity(
        UIElement element,
        double toOpacity,
        Duration duration,
        IEasingFunction easingFunction,
        Action? onCompleted = null)
    {
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

    private void AnimateSliceClickDown(UIElement target, Duration duration, IEasingFunction easingFunction)
    {
        if (target.RenderTransform is not ScaleTransform scaleTransform)
        {
            scaleTransform = new ScaleTransform(1, 1);
            target.RenderTransform = scaleTransform;
            target.RenderTransformOrigin = new Point(0.5, 0.5);
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

    private void AnimateSliceClickUp(UIElement target, Duration duration, IEasingFunction easingFunction)
    {
        if (target.RenderTransform is not ScaleTransform scaleTransform)
        {
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

    private Point GetTextPosition(Point center, double radius, double startAngle, double endAngle)
    {
        var midAngle = (startAngle + endAngle) / 2;

        var x = center.X + (radius * Math.Cos(midAngle * Math.PI / 180));
        var y = center.Y + (radius * Math.Sin(midAngle * Math.PI / 180));

        return SnapPoint(new Point(x, y));
    }

    private Path CreateSlice(Point center, double outerRadius, double innerRadius, double startAngle, double endAngle)
    {
        var startPointOuter = GetPointOnCircle(center, outerRadius, startAngle);
        var endPointOuter = GetPointOnCircle(center, outerRadius, endAngle);
        var startPointInner = GetPointOnCircle(center, innerRadius, startAngle);
        var endPointInner = GetPointOnCircle(center, innerRadius, endAngle);

        var figure = new PathFigure { StartPoint = startPointInner };

        figure.Segments.Add(new LineSegment(startPointOuter, true));

        figure.Segments.Add(new ArcSegment(
            endPointOuter,
            new Size(outerRadius, outerRadius),
            0,
            endAngle - startAngle > 180,
            SweepDirection.Clockwise,
            true));

        figure.Segments.Add(new LineSegment(endPointInner, true));

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
        return SnapPoint(new Point(x, y));
    }

    private Point SnapPoint(Point point)
    {
        return new Point(
            SnapToDevicePixel(point.X, isXAxis: true),
            SnapToDevicePixel(point.Y, isXAxis: false));
    }

    private double SnapToDevicePixel(double value, bool isXAxis)
    {
        var dpiInfo = VisualTreeHelper.GetDpi(this);
        var scale = isXAxis ? dpiInfo.DpiScaleX : dpiInfo.DpiScaleY;
        if (scale <= 0)
        {
            return value;
        }

        return Math.Round(value * scale) / scale;
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var r = (byte)Math.Round((from.R * (1 - amount)) + (to.R * amount));
        var g = (byte)Math.Round((from.G * (1 - amount)) + (to.G * amount));
        var b = (byte)Math.Round((from.B * (1 - amount)) + (to.B * amount));
        return Color.FromRgb(r, g, b);
    }

    private static bool IsAppDarkModeEnabled()
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

    /// <summary>
    /// Occurs when a slice is clicked.
    /// </summary>
    public event EventHandler<SliceClickEventArgs> SliceClicked;

    /// <summary>
    /// Occurs when the center close target is clicked.
    /// </summary>
    public event EventHandler CenterClicked;

    /// <summary>
    /// Occurs when a slice edit is requested from the context menu.
    /// </summary>
    public event EventHandler<SliceClickEventArgs> SliceEditRequested;
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
