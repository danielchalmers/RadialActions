using System.Windows;
using System.Windows.Media;

namespace RadialActions.Tests;

public class PieLayoutCalculatorTests
{
    private static readonly Point Center = new(200, 200);
    private const double OuterRadius = 199.5;
    private const double InnerRadius = 50;

    private static Geometry CreateSlice(double startAngle, double endAngle, double innerRadius = InnerRadius)
    {
        return PieLayoutCalculator.CreateSliceGeometry(
            Center,
            OuterRadius,
            innerRadius,
            startAngle,
            endAngle,
            (center, radius, angle) => PieLayoutCalculator.GetPointOnCircle(center, radius, angle, static point => point));
    }

    [Fact]
    public void CreateSliceGeometry_SingleSlice_IsAFullRingThatCanBeHit()
    {
        var geometry = CreateSlice(-90, 270);

        var expectedArea = Math.PI * ((OuterRadius * OuterRadius) - (InnerRadius * InnerRadius));
        Assert.InRange(geometry.GetArea(), expectedArea * 0.99, expectedArea * 1.01);

        // Points off the 12 o'clock seam, where a degenerate wedge would still report a hit.
        Assert.True(geometry.FillContains(new Point(200, 325)));
        Assert.True(geometry.FillContains(new Point(75, 200)));
        Assert.False(geometry.FillContains(Center));
        Assert.True(geometry.IsFrozen);
    }

    [Fact]
    public void CreateSliceGeometry_SingleSliceWithoutHub_IsAFullDisc()
    {
        var geometry = CreateSlice(-90, 270, innerRadius: 0);

        var expectedArea = Math.PI * OuterRadius * OuterRadius;
        Assert.InRange(geometry.GetArea(), expectedArea * 0.99, expectedArea * 1.01);
        Assert.True(geometry.FillContains(Center));
    }

    [Fact]
    public void CreateSliceGeometry_TwoSlices_AreHalfRings()
    {
        var right = CreateSlice(-90, 90);
        var left = CreateSlice(90, 270);

        var halfArea = Math.PI * ((OuterRadius * OuterRadius) - (InnerRadius * InnerRadius)) / 2;
        Assert.InRange(right.GetArea(), halfArea * 0.99, halfArea * 1.01);
        Assert.InRange(left.GetArea(), halfArea * 0.99, halfArea * 1.01);
        Assert.True(right.FillContains(new Point(325, 200)));
        Assert.False(right.FillContains(new Point(75, 200)));
        Assert.True(left.FillContains(new Point(75, 200)));
    }

    [Fact]
    public void CreateArcGeometry_FullSweep_IsACircle()
    {
        var geometry = PieLayoutCalculator.CreateArcGeometry(Center, 197, -90, 270);

        var bounds = geometry.Bounds;
        Assert.Equal(3, bounds.Left, precision: 3);
        Assert.Equal(397, bounds.Right, precision: 3);
        Assert.Equal(3, bounds.Top, precision: 3);
        Assert.Equal(397, bounds.Bottom, precision: 3);
    }

    [Fact]
    public void CreateArcGeometry_QuarterSweep_RunsClockwiseFromTopToRight()
    {
        var geometry = PieLayoutCalculator.CreateArcGeometry(Center, 197, -90, 0);

        var bounds = geometry.Bounds;
        Assert.Equal(200, bounds.Left, precision: 3);
        Assert.Equal(3, bounds.Top, precision: 3);
        Assert.Equal(397, bounds.Right, precision: 3);
        Assert.Equal(200, bounds.Bottom, precision: 3);
    }

    [Fact]
    public void TryGetSelectionArc_FullSweep_CoversTheWholeRimInsideTheStroke()
    {
        var found = PieLayoutCalculator.TryGetSelectionArc(OuterRadius, strokeThickness: 1, arcThickness: 3, separatorGap: 2, -90, 270, out var arc);

        Assert.True(found);
        Assert.Equal(197, arc.Radius, precision: 6);
        Assert.Equal(-90, arc.StartAngle);
        Assert.Equal(270, arc.EndAngle);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(12)]
    public void TryGetSelectionArc_KeepsRoundCapsClearOfBothSeparators(int sliceCount)
    {
        const double strokeThickness = 1;
        const double arcThickness = 3;
        const double separatorGap = 2;
        var step = 360.0 / sliceCount;

        var found = PieLayoutCalculator.TryGetSelectionArc(OuterRadius, strokeThickness, arcThickness, separatorGap, -90, -90 + step, out var arc);

        Assert.True(found);
        var startInset = arc.StartAngle - -90;
        var endInset = (-90 + step) - arc.EndAngle;
        Assert.Equal(startInset, endInset, precision: 6);

        // Distance from the separator line to the edge of the cap, minus half the separator stroke, is the visible gap.
        var visibleGap = (arc.Radius * Math.Sin(startInset * Math.PI / 180)) - (arcThickness / 2) - (strokeThickness / 2);
        Assert.Equal(separatorGap, visibleGap, precision: 6);
    }

    [Fact]
    public void TryGetSelectionArc_SliceTooNarrow_ReturnsFalse()
    {
        var found = PieLayoutCalculator.TryGetSelectionArc(OuterRadius, strokeThickness: 1, arcThickness: 3, separatorGap: 2, -90, -89, out _);

        Assert.False(found);
    }

    [Theory]
    [InlineData(100, 1)]
    [InlineData(199.5, 1)]
    [InlineData(240, 1.2)]
    [InlineData(280, 1.4)]
    [InlineData(499.5, 1.4)]
    public void GetContentScale_GrowsWithLargeMenusOnly(double outerRadius, double expectedScale)
    {
        Assert.Equal(expectedScale, PieLayoutCalculator.GetContentScale(outerRadius), precision: 6);
    }

    [Theory]
    [InlineData(199.5, 11, 185.25)]
    [InlineData(129.5, 11, 115.25)]
    [InlineData(279.5, 15.4, 261.95)]
    public void GetRimHintRadius_KeepsAConstantInsetInsideTheSelectionArc(double outerRadius, double hintFontSize, double expectedRadius)
    {
        var radius = PieLayoutCalculator.GetRimHintRadius(outerRadius, strokeThickness: 1, arcThickness: 3, hintFontSize);

        Assert.Equal(expectedRadius, radius, precision: 6);
    }

    // Expected values come from the label-fit simulation in the design audit (notes-pie-visual.md): a 15px label row 12.5px below the mid-ring content center, 3px padding, no hint keep-out.
    [Theory]
    [InlineData(400, 12, 0, 44.5)]
    [InlineData(400, 12, 1, 47.9)]
    [InlineData(400, 12, 2, 134.5)]
    [InlineData(400, 12, 5, 58.6)]
    [InlineData(260, 5, 0, 89.2)]
    [InlineData(260, 5, 1, 81.2)]
    [InlineData(260, 5, 2, 117.5)]
    [InlineData(400, 2, 0, 141.5)]
    [InlineData(400, 2, 1, 141.5)]
    public void GetLabelMaxWidth_FitsTheRowInsideTheAnnularSector(double size, int sliceCount, int sliceIndex, double expectedWidth)
    {
        var width = GetSimulatedLabelWidth(size, sliceCount, sliceIndex, Rect.Empty);

        Assert.Equal(expectedWidth, width, precision: 1);
    }

    [Fact]
    public void GetLabelMaxWidth_FiveSlicesAtSmallestPreset_FitsMoreThanTheOldFixedFloor()
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.True(GetSimulatedLabelWidth(260, 5, i, Rect.Empty) >= 81);
        }
    }

    [Fact]
    public void GetLabelMaxWidth_StopsBeforeTheKeepOut()
    {
        // A digit hint near the rim on the right half of a two-slice menu, overlapping the label row vertically.
        var keepOut = new Rect(176, 5, 15, 20);

        var width = GetSimulatedLabelWidth(400, 2, 0, keepOut);

        var rowCenterX = GetMidRingRadius(400);
        Assert.True(width > 0);
        Assert.True(rowCenterX + (width / 2) <= keepOut.Left + 1e-6);
        Assert.True(width < 141.5);
    }

    [Fact]
    public void GetLabelMaxWidth_RowThatCannotFit_ReturnsZero()
    {
        // A row centered inside the hub can't be placed at any width.
        var width = PieLayoutCalculator.GetLabelMaxWidth(new Vector(0, 0), 15, InnerRadius, OuterRadius, -90, 90, 3, Rect.Empty);

        Assert.Equal(0, width);
    }

    [Theory]
    [InlineData(300, 100, 0)]
    [InlineData(300, 300, 1)]
    [InlineData(100, 300, 2)]
    [InlineData(100, 100, 3)]
    [InlineData(201, 60, 0)]
    [InlineData(199, 60, 3)]
    public void GetSliceIndexAt_CountsClockwiseFromTwelveOClock(double x, double y, int expectedIndex)
    {
        var index = PieLayoutCalculator.GetSliceIndexAt(Center, InnerRadius, OuterRadius, 4, new Point(x, y));

        Assert.Equal(expectedIndex, index);
    }

    [Theory]
    [InlineData(200, 210)]
    [InlineData(200, 450)]
    [InlineData(10, 10)]
    public void GetSliceIndexAt_OverTheHubOrOutsideTheRing_ReturnsNoSlice(double x, double y)
    {
        var index = PieLayoutCalculator.GetSliceIndexAt(Center, InnerRadius, OuterRadius, 4, new Point(x, y));

        Assert.Equal(-1, index);
    }

    [Fact]
    public void GetSliceIndexAt_SingleSlice_CoversTheWholeRing()
    {
        Assert.Equal(0, PieLayoutCalculator.GetSliceIndexAt(Center, InnerRadius, OuterRadius, 1, new Point(200, 325)));
        Assert.Equal(0, PieLayoutCalculator.GetSliceIndexAt(Center, InnerRadius, OuterRadius, 1, new Point(75, 200)));
    }

    private static double GetMidRingRadius(double size)
    {
        var canvasRadius = size / 2;
        return ((canvasRadius - 0.5) + (canvasRadius * 0.25)) / 2;
    }

    private static double GetSimulatedLabelWidth(double size, int sliceCount, int sliceIndex, Rect keepOut)
    {
        var canvasRadius = size / 2;
        var outerRadius = canvasRadius - 0.5;
        var innerRadius = canvasRadius * 0.25;
        var textRadius = GetMidRingRadius(size);
        var step = 360.0 / sliceCount;
        var startAngle = (sliceIndex * step) - 90;
        var endAngle = startAngle + step;
        var midAngle = ((startAngle + endAngle) / 2) * Math.PI / 180;
        var rowCenter = new Vector(textRadius * Math.Cos(midAngle), (textRadius * Math.Sin(midAngle)) + 12.5);

        return PieLayoutCalculator.GetLabelMaxWidth(rowCenter, 15, innerRadius, outerRadius, startAngle, endAngle, 3, keepOut);
    }
}
