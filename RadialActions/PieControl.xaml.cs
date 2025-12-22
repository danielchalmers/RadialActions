using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;
using RadialActions.Properties;

namespace RadialActions;

/// <summary>
/// A radial pie menu control that displays clickable slices.
/// </summary>
public partial class PieControl : UserControl
{
    private const string DefaultSliceColorHex = "#2D2D30";
    private const string DefaultSliceHoverColorHex = "#3E3E42";
    private const string DefaultSliceBorderColorHex = "#1E1E1E";
    private const string DefaultTextColorHex = "#FFFFFF";

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
        var settings = Settings.Default;

        PieCanvas.Width = canvasSize;
        PieCanvas.Height = canvasSize;

        // Parse colors from settings
        var accentColor = SystemParameters.WindowGlassColor;
        var sliceColor = ParseColor(settings.SliceColor, accentColor);
        if (IsDefaultColor(settings.SliceColor, DefaultSliceColorHex))
        {
            sliceColor = accentColor;
        }

        var hoverColor = ParseColor(settings.SliceHoverColor, BlendColor(sliceColor, Colors.White, 0.15));
        if (IsDefaultColor(settings.SliceHoverColor, DefaultSliceHoverColorHex))
        {
            hoverColor = BlendColor(sliceColor, Colors.White, 0.15);
        }

        var borderColor = ParseColor(settings.SliceBorderColor, BlendColor(sliceColor, Colors.Black, 0.35));
        if (IsDefaultColor(settings.SliceBorderColor, DefaultSliceBorderColorHex))
        {
            borderColor = BlendColor(sliceColor, Colors.Black, 0.35);
        }
        var textColor = ParseColor(settings.TextColor, GetReadableTextColor(sliceColor));
        if (IsDefaultColor(settings.TextColor, DefaultTextColorHex))
        {
            textColor = GetReadableTextColor(sliceColor);
        }

        var innerRadius = settings.ShowCenterHole ? canvasRadius * settings.CenterHoleRatio : 0;
        var angleStep = 360.0 / Slices.Count;

        // Draw center hole background if enabled
        if (settings.ShowCenterHole && innerRadius > 0)
        {
            var centerHole = new Ellipse
            {
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Fill = new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)),
                Stroke = new SolidColorBrush(borderColor),
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(centerHole, center.X - innerRadius);
            Canvas.SetTop(centerHole, center.Y - innerRadius);
            Panel.SetZIndex(centerHole, 10);
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
            var fillBrush = new SolidColorBrush(sliceColor);
            slice.Fill = fillBrush;
            slice.Stroke = new SolidColorBrush(borderColor);
            slice.StrokeThickness = 2;
            slice.Cursor = System.Windows.Input.Cursors.Hand;

            var isMouseDown = false;
            var sliceIndex = i;

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

            // Add icon if enabled
            if (settings.ShowIcons && !string.IsNullOrEmpty(sliceAction.Icon))
            {
                var iconText = new TextBlock
                {
                    Text = sliceAction.Icon,
                    Foreground = new SolidColorBrush(textColor),
                    FontSize = settings.ShowLabels ? 20 : 28,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                };

                iconText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var iconSize = iconText.DesiredSize;

                var iconOffset = settings.ShowLabels ? -12 : 0;
                Canvas.SetLeft(iconText, textPosition.X - (iconSize.Width / 2));
                Canvas.SetTop(iconText, textPosition.Y - (iconSize.Height / 2) + iconOffset);

                PieCanvas.Children.Add(iconText);
            }

            // Add text label if enabled
            if (settings.ShowLabels)
            {
                var text = new TextBlock
                {
                    Text = sliceAction.Name,
                    Foreground = new SolidColorBrush(textColor),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = canvasRadius * 0.4,
                };

                text.Measure(new Size(text.MaxWidth, double.PositiveInfinity));
                var textSize = text.DesiredSize;

                var textOffset = settings.ShowIcons ? 10 : 0;
                Canvas.SetLeft(text, textPosition.X - (textSize.Width / 2));
                Canvas.SetTop(text, textPosition.Y - (textSize.Height / 2) + textOffset);

                PieCanvas.Children.Add(text);
            }
        }
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            if (string.IsNullOrEmpty(hex))
                return fallback;

            var color = ColorConverter.ConvertFromString(hex);
            return color is Color c ? c : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static bool IsDefaultColor(string hex, string defaultHex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return true;

        return string.Equals(hex.Trim(), defaultHex, StringComparison.OrdinalIgnoreCase);
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var r = (byte)Math.Round((from.R * (1 - amount)) + (to.R * amount));
        var g = (byte)Math.Round((from.G * (1 - amount)) + (to.G * amount));
        var b = (byte)Math.Round((from.B * (1 - amount)) + (to.B * amount));
        return Color.FromRgb(r, g, b);
    }

    private static Color GetReadableTextColor(Color background)
    {
        var luminance = (0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B) / 255.0;
        return luminance > 0.6
            ? Color.FromRgb(28, 28, 28)
            : Color.FromRgb(245, 245, 245);
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

    private void AnimateSliceClickDown(Path slice)
    {
        var scaleTransform = new ScaleTransform(1, 1);
        slice.RenderTransform = scaleTransform;
        slice.RenderTransformOrigin = new Point(0.5, 0.5);

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

    private void AnimateSliceClickUp(Path slice)
    {
        if (slice.RenderTransform is not ScaleTransform scaleTransform)
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
