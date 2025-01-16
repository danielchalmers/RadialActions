using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
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
    // Dependency Property: Number of Slices
    public static readonly DependencyProperty SliceCountProperty =
        DependencyProperty.Register("SliceCount", typeof(int), typeof(InteractivePie),
            new PropertyMetadata(6, OnSliceCountChanged));

    public int SliceCount
    {
        get => (int)GetValue(SliceCountProperty);
        set => SetValue(SliceCountProperty, value);
    }

    // Triggered when the SliceCount changes
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

    // Creates the pie menu
    private void CreatePieMenu()
    {
        PieMenuCanvas.Children.Clear();
        double canvasRadius = Math.Min(ActualWidth, ActualHeight) / 2;
        Point center = new Point(canvasRadius, canvasRadius);

        double angleStep = 360.0 / SliceCount;
        for (int i = 0; i < SliceCount; i++)
        {
            double startAngle = i * angleStep;
            double endAngle = startAngle + angleStep;

            Path slice = CreateSlice(center, canvasRadius, startAngle, endAngle);
            slice.Fill = new SolidColorBrush(GetSliceColor(i));
            slice.Stroke = Brushes.Black;
            slice.StrokeThickness = 1;

            // Add hover effect using triggers
            slice.MouseEnter += (s, e) =>
            {
                slice.Fill = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            };
            slice.MouseLeave += (s, e) =>
            {
                slice.Fill = new SolidColorBrush(GetSliceColor(i));
            };

            slice.MouseLeftButtonDown += (s, e) =>
            {
                SliceClicked?.Invoke(this, new SliceClickEventArgs(i + 1));
            };

            PieMenuCanvas.Children.Add(slice);
        }
    }

    // Creates a pie slice
    private Path CreateSlice(Point center, double radius, double startAngle, double endAngle)
    {
        Point startPoint = GetPointOnCircle(center, radius, startAngle);
        Point endPoint = GetPointOnCircle(center, radius, endAngle);

        PathFigure figure = new PathFigure
        {
            StartPoint = center
        };
        figure.Segments.Add(new LineSegment(startPoint, true));
        figure.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0, endAngle - startAngle > 180, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(center, true));

        PathGeometry geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Path { Data = geometry };
    }

    // Calculates a point on the circle given an angle
    private Point GetPointOnCircle(Point center, double radius, double angleInDegrees)
    {
        double angleInRadians = angleInDegrees * Math.PI / 180;
        double x = center.X + radius * Math.Cos(angleInRadians);
        double y = center.Y + radius * Math.Sin(angleInRadians);
        return new Point(x, y);
    }

    // Generates a color for the slice
    private Color GetSliceColor(int index)
    {
        return Color.FromRgb((byte)(100 + index * 20), (byte)(150 - index * 15), (byte)(200 - index * 10));
    }

    // Event: Slice Clicked
    public event EventHandler<SliceClickEventArgs> SliceClicked;

    // Custom Event Args for Slice Clicks
    public class SliceClickEventArgs : EventArgs
    {
        public int SliceNumber { get; }

        public SliceClickEventArgs(int sliceNumber)
        {
            SliceNumber = sliceNumber;
        }
    }
}
