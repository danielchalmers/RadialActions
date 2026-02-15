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

    public PieControl()
    {
        InitializeComponent();
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
        var accentColor = SystemParameters.WindowGlassColor;
        var isHighContrast = SystemParameters.HighContrast;
        var sliceColor = isHighContrast
            ? SystemColors.WindowColor
            : GetNeutralSliceBackgroundColor();
        var iconAndTextColor = isHighContrast
            ? SystemColors.WindowTextColor
            : GetAccessibleAccentColor(accentColor, sliceColor);
        var isDarkSlice = GetRelativeLuminance(sliceColor) < 0.5;
        var hoverTarget = isDarkSlice ? Colors.White : Colors.Black;
        var borderTarget = isDarkSlice ? Colors.White : Colors.Black;
        var hoverColor = isHighContrast
            ? SystemColors.HighlightColor
            : BlendColor(sliceColor, hoverTarget, 0.12);
        var borderColor = isHighContrast
            ? SystemColors.WindowTextColor
            : BlendColor(sliceColor, borderTarget, 0.25);
        var centerHoleColor = isHighContrast
            ? SystemColors.ControlColor
            : BlendColor(sliceColor, borderTarget, 0.2);
        var centerHoleHoverColor = isHighContrast
            ? SystemColors.HighlightColor
            : BlendColor(centerHoleColor, hoverTarget, 0.2);
        var centerHoleHoverBorderColor = isHighContrast
            ? SystemColors.HighlightTextColor
            : BlendColor(borderColor, borderTarget, 0.35);

        if (isHighContrast && hoverColor == sliceColor)
        {
            hoverColor = BlendColor(sliceColor, borderColor, 0.3);
        }
        var showCenterHole = true;
        var showIcons = true;
        var showLabels = true;

        PieCanvas.Width = canvasSize;
        PieCanvas.Height = canvasSize;

        var innerRadius = showCenterHole ? canvasRadius * DefaultCenterHoleRatio : 0;
        var angleStep = 360.0 / Slices.Count;

        // Draw interactive center close target.
        if (showCenterHole && innerRadius > 0)
        {
            var centerFillBrush = new SolidColorBrush(centerHoleColor);
            var centerStrokeBrush = new SolidColorBrush(borderColor);
            var centerHole = new Ellipse
            {
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Fill = centerFillBrush,
                Stroke = centerStrokeBrush,
                StrokeThickness = 2,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var centerCloseIcon = new TextBlock
            {
                Text = "\uE8BB",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Foreground = new SolidColorBrush(iconAndTextColor),
                FontSize = Math.Max(innerRadius * 0.42, 11),
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0),
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };

            var centerCloseTarget = new Grid
            {
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            centerCloseTarget.Children.Add(centerHole);
            centerCloseTarget.Children.Add(centerCloseIcon);

            var isCenterMouseDown = false;

            centerCloseTarget.MouseEnter += (s, e) =>
            {
                AnimateSliceColor(centerFillBrush, centerHoleHoverColor, 0.08);
                AnimateSliceColor(centerStrokeBrush, centerHoleHoverBorderColor, 0.08);
                centerCloseIcon.Visibility = Visibility.Visible;
                AnimateOpacity(centerCloseIcon, 1, 0.08);
            };

            centerCloseTarget.MouseLeave += (s, e) =>
            {
                AnimateSliceColor(centerFillBrush, centerHoleColor, 0.1);
                AnimateSliceColor(centerStrokeBrush, borderColor, 0.1);
                AnimateOpacity(centerCloseIcon, 0, 0.08, () =>
                {
                    if (!centerCloseTarget.IsMouseOver)
                    {
                        centerCloseIcon.Visibility = Visibility.Collapsed;
                    }
                });

                if (isCenterMouseDown)
                {
                    isCenterMouseDown = false;
                    AnimateSliceClickUp(centerCloseTarget);
                }
            };

            centerCloseTarget.MouseLeftButtonDown += (s, e) =>
            {
                isCenterMouseDown = true;
                AnimateSliceClickDown(centerCloseTarget);
                e.Handled = true;
            };

            centerCloseTarget.MouseLeftButtonUp += (s, e) =>
            {
                if (isCenterMouseDown)
                {
                    isCenterMouseDown = false;
                    AnimateSliceClickUp(centerCloseTarget);
                    CenterClicked?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            };

            Canvas.SetLeft(centerCloseTarget, center.X - innerRadius);
            Canvas.SetTop(centerCloseTarget, center.Y - innerRadius);
            Panel.SetZIndex(centerCloseTarget, 10);
            PieCanvas.Children.Add(centerCloseTarget);
        }

        for (var i = 0; i < Slices.Count; i++)
        {
            var sliceAction = Slices[i];
            var startAngle = (i * angleStep) - 90; // Start from top
            var endAngle = startAngle + angleStep;

            // Create the pie slice
            var slice = CreateSlice(center, canvasRadius, innerRadius, startAngle, endAngle);

            // Use a SolidColorBrush for animation
            var fillBrush = new SolidColorBrush(sliceColor);
            slice.Fill = fillBrush;
            slice.Stroke = new SolidColorBrush(borderColor);
            slice.StrokeThickness = 2;
            slice.Cursor = System.Windows.Input.Cursors.Hand;

            var isMouseDown = false;

            // Add mouse-down animation
            slice.MouseLeftButtonDown += (s, e) =>
            {
                isMouseDown = true;
                AnimateSliceClickDown(slice);
                e.Handled = true;
            };

            // Add mouse-up animation
            slice.MouseLeftButtonUp += (s, e) =>
            {
                if (isMouseDown)
                {
                    isMouseDown = false;
                    AnimateSliceClickUp(slice);
                    SliceClicked?.Invoke(this, new SliceClickEventArgs(sliceAction));
                    e.Handled = true;
                }
            };

            // Add hover effects
            slice.MouseEnter += (s, e) =>
            {
                AnimateSliceColor(fillBrush, hoverColor, 0.1);
            };
            slice.MouseLeave += (s, e) =>
            {
                AnimateSliceColor(fillBrush, sliceColor, 0.1);

                if (isMouseDown)
                {
                    isMouseDown = false;
                    AnimateSliceClickUp(slice);
                }
            };

            var contextMenu = new ContextMenu();
            var editMenuItem = new MenuItem { Header = "Edit..." };
            editMenuItem.Click += (s, e) => SliceEditRequested?.Invoke(this, new SliceClickEventArgs(sliceAction));
            contextMenu.Items.Add(editMenuItem);
            slice.ContextMenu = contextMenu;

            PieCanvas.Children.Add(slice);

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
                };

                if (showIcon)
                {
                    var iconText = new TextBlock
                    {
                        Text = sliceAction.Icon,
                        Foreground = new SolidColorBrush(iconAndTextColor),
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
                        Foreground = new SolidColorBrush(iconAndTextColor),
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

                Canvas.SetLeft(contentPanel, textPosition.X - (contentSize.Width / 2));
                Canvas.SetTop(contentPanel, textPosition.Y - (contentSize.Height / 2));

                PieCanvas.Children.Add(contentPanel);
            }
        }
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var r = (byte)Math.Round((from.R * (1 - amount)) + (to.R * amount));
        var g = (byte)Math.Round((from.G * (1 - amount)) + (to.G * amount));
        var b = (byte)Math.Round((from.B * (1 - amount)) + (to.B * amount));
        return Color.FromRgb(r, g, b);
    }

    private static Color GetNeutralSliceBackgroundColor()
    {
        var dark = Color.FromRgb(30, 30, 30);
        var light = Color.FromRgb(242, 242, 242);
        return IsAppDarkModeEnabled() ? dark : light;
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

    private void AnimateSliceColor(SolidColorBrush brush, Color toColor, double durationInSeconds)
    {
        var colorAnimation = new ColorAnimation
        {
            To = toColor,
            Duration = TimeSpan.FromSeconds(durationInSeconds),
            EasingFunction = new QuadraticEase()
        };

        brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
    }

    private void AnimateOpacity(UIElement element, double toOpacity, double durationInSeconds, Action? onCompleted = null)
    {
        var opacityAnimation = new DoubleAnimation
        {
            To = toOpacity,
            Duration = TimeSpan.FromSeconds(durationInSeconds),
            EasingFunction = new QuadraticEase()
        };

        if (onCompleted != null)
        {
            opacityAnimation.Completed += (s, e) => onCompleted();
        }

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
    }

    private void AnimateSliceClickDown(UIElement target)
    {
        if (target.RenderTransform is not ScaleTransform scaleTransform)
        {
            scaleTransform = new ScaleTransform(1, 1);
            target.RenderTransform = scaleTransform;
            target.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var scaleAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0.95,
            Duration = TimeSpan.FromMilliseconds(75),
            FillBehavior = FillBehavior.HoldEnd
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
    }

    private void AnimateSliceClickUp(UIElement target)
    {
        if (target.RenderTransform is not ScaleTransform scaleTransform)
            return;

        var scaleAnimation = new DoubleAnimation
        {
            From = 0.95,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(75),
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
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
