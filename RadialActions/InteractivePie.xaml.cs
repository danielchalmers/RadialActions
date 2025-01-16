using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RadialActions;

/// <summary>
/// Interaction logic for InteractivePie.xaml
/// </summary>
public partial class InteractivePie : UserControl
{
    public InteractivePie()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty SliceCountProperty =
        DependencyProperty.Register("SliceCount", typeof(int), typeof(InteractivePie),
            new PropertyMetadata(6, OnSliceCountChanged));

    public int SliceCount
    {
        get => (int)GetValue(SliceCountProperty);
        set => SetValue(SliceCountProperty, value);
    }

    private static void OnSliceCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InteractivePie control)
        {
            control.CreatePieMenu();
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        CreatePieMenu();
    }

    private void CreatePieMenu()
    {
        PieMenuCanvas.Children.Clear();
        var canvasSize = Math.Min(ActualWidth, ActualHeight);
        var canvasRadius = canvasSize / 2;
        var center = new Point(canvasRadius, canvasRadius);

        PieMenuCanvas.Width = canvasSize;
        PieMenuCanvas.Height = canvasSize;

        var angleStep = 360.0 / SliceCount;
        for (var i = 0; i < SliceCount; i++)
        {
            var sliceIndex = i; // Create a local copy for the current slice number
            var startAngle = i * angleStep;
            var endAngle = startAngle + angleStep;

            // Create the pie slice
            var slice = CreateSlice(center, canvasRadius, startAngle, endAngle);
            slice.Fill = new SolidColorBrush(GetSliceColor(i));
            slice.Stroke = Brushes.Black;
            slice.StrokeThickness = 1;

            // Add hover effect
            slice.MouseEnter += (s, e) =>
            {
                slice.Fill = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            };
            slice.MouseLeave += (s, e) =>
            {
                slice.Fill = new SolidColorBrush(GetSliceColor(i));
            };

            // Handle click events
            slice.MouseLeftButtonDown += (s, e) =>
            {
                SliceClicked?.Invoke(this, new SliceClickEventArgs(sliceIndex));
            };

            PieMenuCanvas.Children.Add(slice);

            // Add text to the slice
            var text = new TextBlock
            {
                Text = $"Slice {sliceIndex}",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Calculate the center position of the slice
            var textPosition = GetTextPosition(center, canvasRadius, startAngle, endAngle);

            // Measure the TextBlock's size
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textSize = text.DesiredSize;

            // Adjust position to center the text
            Canvas.SetLeft(text, textPosition.X - (textSize.Width / 2));
            Canvas.SetTop(text, textPosition.Y - (textSize.Height / 2));

            PieMenuCanvas.Children.Add(text);
        }
    }

    private Point GetTextPosition(Point center, double radius, double startAngle, double endAngle)
    {
        var midAngle = (startAngle + endAngle) / 2;
        var textRadius = radius * 0.6; // Adjust to move text closer to the center.

        var x = center.X + (textRadius * Math.Cos(midAngle * Math.PI / 180));
        var y = center.Y + (textRadius * Math.Sin(midAngle * Math.PI / 180));

        return new(x, y);
    }

    private Path CreateSlice(Point center, double radius, double startAngle, double endAngle)
    {
        var startPoint = GetPointOnCircle(center, radius, startAngle);
        var endPoint = GetPointOnCircle(center, radius, endAngle);

        var figure = new PathFigure
        {
            StartPoint = center
        };

        figure.Segments.Add(new LineSegment(startPoint, true));
        figure.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0, endAngle - startAngle > 180, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(center, true));

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

    private Color GetSliceColor(int index)
    {
        return Color.FromRgb((byte)(100 + (index * 20)), (byte)(150 - (index * 15)), (byte)(200 - (index * 10)));
    }

    public event EventHandler<SliceClickEventArgs> SliceClicked;

    public class SliceClickEventArgs(int sliceNumber) : EventArgs
    {
        public int SliceNumber { get; } = sliceNumber;
    }
}
