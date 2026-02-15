using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RadialActions;

public static class PieLayoutCalculator
{
    public readonly record struct PieLayout(
        double CanvasSize,
        double CanvasRadius,
        Point Center,
        double InnerRadius,
        double OuterRadius,
        double AngleStep);

    public static bool TryCreateLayout(
        double actualWidth,
        double actualHeight,
        int sliceCount,
        double centerHoleRatio,
        double strokeThickness,
        Func<double, bool, double> snapToDevicePixel,
        out PieLayout layout)
    {
        layout = default;
        if (sliceCount <= 0 || actualWidth <= 0 || actualHeight <= 0)
        {
            return false;
        }

        var canvasSize = snapToDevicePixel(Math.Min(actualWidth, actualHeight), true);
        if (canvasSize <= 0)
        {
            return false;
        }

        var canvasRadius = canvasSize / 2;
        var center = new Point(canvasRadius, canvasRadius);
        var innerRadius = canvasRadius * centerHoleRatio;
        var outerRadius = Math.Max(0, canvasRadius - (strokeThickness / 2));
        var angleStep = 360.0 / sliceCount;

        layout = new PieLayout(canvasSize, canvasRadius, center, innerRadius, outerRadius, angleStep);
        return true;
    }

    public static double NormalizeSignedAngle(double angle)
    {
        while (angle <= -180)
        {
            angle += 360;
        }

        while (angle > 180)
        {
            angle -= 360;
        }

        return angle;
    }

    public static Point GetTextPosition(
        Point center,
        double radius,
        double startAngle,
        double endAngle,
        Func<Point, Point> snapPoint)
    {
        var midAngle = (startAngle + endAngle) / 2;
        var x = center.X + (radius * Math.Cos(midAngle * Math.PI / 180));
        var y = center.Y + (radius * Math.Sin(midAngle * Math.PI / 180));
        return snapPoint(new Point(x, y));
    }

    public static Path CreateSlice(
        Point center,
        double outerRadius,
        double innerRadius,
        double startAngle,
        double endAngle,
        Func<Point, double, double, Point> getPointOnCircle)
    {
        var startPointOuter = getPointOnCircle(center, outerRadius, startAngle);
        var endPointOuter = getPointOnCircle(center, outerRadius, endAngle);
        var startPointInner = getPointOnCircle(center, innerRadius, startAngle);
        var endPointInner = getPointOnCircle(center, innerRadius, endAngle);

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

    public static Point GetPointOnCircle(
        Point center,
        double radius,
        double angleInDegrees,
        Func<Point, Point> snapPoint)
    {
        var angleInRadians = angleInDegrees * Math.PI / 180;
        var x = center.X + (radius * Math.Cos(angleInRadians));
        var y = center.Y + (radius * Math.Sin(angleInRadians));
        return snapPoint(new Point(x, y));
    }
}
