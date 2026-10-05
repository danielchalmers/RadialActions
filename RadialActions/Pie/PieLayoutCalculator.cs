using System.Windows;
using System.Windows.Media;

namespace RadialActions;

public static class PieLayoutCalculator
{
    // Sweeps at least this wide (one slice) are drawn as a full ring rather than a wedge.
    private const double FullCircleSweep = 360 - 1e-6;

    // Content (icons, labels and hints) is designed at a 200px outer radius and grows with larger menus up to MaxContentScale.
    private const double ContentScaleReferenceRadius = 200;
    private const double MaxContentScale = 1.4;

    // Space between the rim hints and the inner edge of the selection arc so the release glyph never touches the arc.
    private const double RimHintArcGap = 2;

    public readonly record struct PieLayout(
        double CanvasSize,
        double CanvasRadius,
        Point Center,
        double InnerRadius,
        double OuterRadius,
        double AngleStep);

    public readonly record struct ArcSpan(double Radius, double StartAngle, double EndAngle);

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

    /// <summary>
    /// Builds the frozen geometry of one slice: an annular wedge, or a full ring with no seam when the slice spans the whole circle.
    /// </summary>
    public static Geometry CreateSliceGeometry(
        Point center,
        double outerRadius,
        double innerRadius,
        double startAngle,
        double endAngle,
        Func<Point, double, double, Point> getPointOnCircle)
    {
        Geometry geometry;
        if (endAngle - startAngle >= FullCircleSweep)
        {
            // A wedge from an angle back to itself collapses to a zero-area line, which can't be hovered or clicked.
            var outerCircle = new EllipseGeometry(center, outerRadius, outerRadius);
            geometry = innerRadius > 0
                ? new CombinedGeometry(GeometryCombineMode.Exclude, outerCircle, new EllipseGeometry(center, innerRadius, innerRadius))
                : outerCircle;
        }
        else
        {
            geometry = CreateWedgeGeometry(center, outerRadius, innerRadius, startAngle, endAngle, getPointOnCircle);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// Builds a frozen open arc (or a full circle) at <paramref name="radius"/>, meant to be stroked.
    /// </summary>
    public static Geometry CreateArcGeometry(Point center, double radius, double startAngle, double endAngle)
    {
        Geometry geometry;
        if (endAngle - startAngle >= FullCircleSweep)
        {
            geometry = new EllipseGeometry(center, radius, radius);
        }
        else
        {
            var startPoint = GetPointOnCircle(center, radius, startAngle, static point => point);
            var endPoint = GetPointOnCircle(center, radius, endAngle, static point => point);
            var figure = new PathFigure { StartPoint = startPoint, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment(
                endPoint,
                new Size(radius, radius),
                0,
                endAngle - startAngle > 180,
                SweepDirection.Clockwise,
                true));

            var pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(figure);
            geometry = pathGeometry;
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// Places the selection arc of a slice: a band of <paramref name="arcThickness"/> just inside the rim stroke, with round caps kept <paramref name="separatorGap"/> clear of both separators.
    /// </summary>
    /// <returns>False when the slice is too narrow to fit the arc between its separators.</returns>
    public static bool TryGetSelectionArc(
        double outerRadius,
        double strokeThickness,
        double arcThickness,
        double separatorGap,
        double startAngle,
        double endAngle,
        out ArcSpan arc)
    {
        arc = default;
        var radius = outerRadius - strokeThickness - (arcThickness / 2);
        if (radius <= 0 || arcThickness <= 0)
        {
            return false;
        }

        if (endAngle - startAngle >= FullCircleSweep)
        {
            arc = new ArcSpan(radius, startAngle, endAngle);
            return true;
        }

        // The round cap extends half the thickness past the arc's end, and the separator stroke extends half its own thickness.
        var clearance = separatorGap + (strokeThickness / 2) + (arcThickness / 2);
        if (clearance >= radius)
        {
            return false;
        }

        var inset = Math.Asin(clearance / radius) * 180 / Math.PI;
        if (endAngle - startAngle <= 2 * inset)
        {
            return false;
        }

        arc = new ArcSpan(radius, startAngle + inset, endAngle - inset);
        return true;
    }

    /// <summary>
    /// How much icons, labels and hints grow with the menu: 1 up to the default size, capped for very large menus.
    /// </summary>
    public static double GetContentScale(double outerRadius)
    {
        return Math.Clamp(outerRadius / ContentScaleReferenceRadius, 1, MaxContentScale);
    }

    /// <summary>
    /// Radius of the digit and release hints: a constant inset from the rim that clears the selection arc instead of a fraction of the radius, so the gap stays the same at every menu size.
    /// Pass the font size of the largest hint, since every hint is centered on this radius.
    /// </summary>
    public static double GetRimHintRadius(double outerRadius, double strokeThickness, double arcThickness, double hintFontSize)
    {
        return outerRadius - strokeThickness - arcThickness - RimHintArcGap - (hintFontSize * 0.75);
    }

    /// <summary>
    /// Finds the widest label row, centered on <paramref name="rowCenter"/> (relative to the pie center), that stays inside the slice's annular sector: within the outer radius, between both separators and clear of the hub, each by <paramref name="padding"/>, and outside <paramref name="keepOut"/>.
    /// </summary>
    /// <returns>The maximum width, or 0 when not even a sliver of the row fits.</returns>
    public static double GetLabelMaxWidth(
        Vector rowCenter,
        double rowHeight,
        double innerRadius,
        double outerRadius,
        double startAngle,
        double endAngle,
        double padding,
        Rect keepOut)
    {
        const double minimumProbeWidth = 0.01;
        const int searchIterations = 30;

        bool Fits(double width) => LabelRowFits(width, rowCenter, rowHeight, innerRadius, outerRadius, startAngle, endAngle, padding, keepOut);

        if (!Fits(minimumProbeWidth))
        {
            return 0;
        }

        // Every constraint is "the row stays inside a region", so a narrower row fits whenever a wider one does and a binary search is exact.
        var fitting = minimumProbeWidth;
        var failing = (2 * Math.Max(outerRadius, 0)) + minimumProbeWidth;
        for (var i = 0; i < searchIterations; i++)
        {
            var width = (fitting + failing) / 2;
            if (Fits(width))
            {
                fitting = width;
            }
            else
            {
                failing = width;
            }
        }

        return fitting;
    }

    /// <summary>
    /// Finds the slice under <paramref name="point"/> from geometry alone (slices are laid out clockwise from 12 o'clock).
    /// </summary>
    /// <returns>The slice index, or -1 over the hub or outside the ring.</returns>
    public static int GetSliceIndexAt(Point center, double innerRadius, double outerRadius, int sliceCount, Point point)
    {
        if (sliceCount <= 0)
        {
            return -1;
        }

        var distance = (point - center).Length;
        if (distance < innerRadius || distance > outerRadius)
        {
            return -1;
        }

        var angleFromTop = PieReorderCalculator.GetPointerAngle(center, point) + 90;
        angleFromTop = ((angleFromTop % 360) + 360) % 360;
        var index = (int)(angleFromTop / (360.0 / sliceCount));
        return Math.Min(index, sliceCount - 1);
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

    private static PathGeometry CreateWedgeGeometry(
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
        return geometry;
    }

    private static bool LabelRowFits(
        double width,
        Vector rowCenter,
        double rowHeight,
        double innerRadius,
        double outerRadius,
        double startAngle,
        double endAngle,
        double padding,
        Rect keepOut)
    {
        var left = rowCenter.X - (width / 2);
        var right = rowCenter.X + (width / 2);
        var top = rowCenter.Y - (rowHeight / 2);
        var bottom = rowCenter.Y + (rowHeight / 2);
        Span<Vector> corners = [new(left, top), new(right, top), new(left, bottom), new(right, bottom)];

        // The circle is convex, so the row is inside it when all four corners are.
        var outerLimit = outerRadius - padding;
        foreach (var corner in corners)
        {
            if (corner.Length > outerLimit)
            {
                return false;
            }
        }

        // Each separator bounds a half-plane; with y pointing down, the cross product is the signed distance to the separator line (positive on the slice's side).
        // Sweeps above 180 degrees only occur for a single full-ring slice, which has no separators.
        var sweep = endAngle - startAngle;
        if (sweep <= 180)
        {
            var startDirection = new Vector(Math.Cos(startAngle * Math.PI / 180), Math.Sin(startAngle * Math.PI / 180));
            var endDirection = new Vector(Math.Cos(endAngle * Math.PI / 180), Math.Sin(endAngle * Math.PI / 180));
            foreach (var corner in corners)
            {
                if (Vector.CrossProduct(startDirection, corner) < padding || Vector.CrossProduct(corner, endDirection) < padding)
                {
                    return false;
                }
            }
        }

        // The point of the row closest to the center must stay clear of the hub.
        var closest = new Vector(Math.Clamp(0, left, right), Math.Clamp(0, top, bottom));
        if (closest.Length < innerRadius + padding)
        {
            return false;
        }

        return keepOut.IsEmpty || !keepOut.IntersectsWith(new Rect(left, top, right - left, bottom - top));
    }
}
