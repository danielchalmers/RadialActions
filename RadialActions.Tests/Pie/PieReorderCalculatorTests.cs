using System.Windows;

namespace RadialActions.Tests;

public class PieReorderCalculatorTests
{
    [Theory]
    [InlineData(100, 0, 0)]     // right
    [InlineData(0, 100, 90)]    // down
    [InlineData(-100, 0, 180)]  // left
    [InlineData(0, -100, -90)]  // up
    public void GetPointerAngle_MatchesSliceLayoutConvention(double dx, double dy, double expectedAngle)
    {
        var center = new Point(200, 200);

        var angle = PieReorderCalculator.GetPointerAngle(center, new Point(center.X + dx, center.Y + dy));

        Assert.Equal(expectedAngle, angle, precision: 6);
    }

    [Theory]
    [InlineData(0, 0, 0)]       // no rotation stays put
    [InlineData(0, 30, 0)]      // less than half a slot rounds back
    [InlineData(0, 40, 1)]      // more than half a slot advances
    [InlineData(0, 144, 2)]     // two slots clockwise
    [InlineData(0, -72, 4)]     // one slot counterclockwise wraps
    [InlineData(4, 72, 0)]      // last slice wraps forward to first slot
    [InlineData(0, 360, 0)]     // full revolution lands back home
    [InlineData(2, -216, 4)]    // multiple slots counterclockwise wraps
    public void GetTargetSlot_MapsRotationToSlot(int originalIndex, double rotationOffset, int expectedSlot)
    {
        const double angleStep = 72;
        const int sliceCount = 5;

        var slot = PieReorderCalculator.GetTargetSlot(originalIndex, rotationOffset, angleStep, sliceCount);

        Assert.Equal(expectedSlot, slot);
    }

    [Theory]
    [InlineData(0, 360, 0)]  // a full lap over the six-slot editor ring (five slices plus the ghost) lands home
    [InlineData(4, 60, 5)]   // the last slice dragged one slot clockwise lands in the ghost slot, which the control clamps to the last real slot
    [InlineData(0, -60, 5)]  // the first slice dragged counterclockwise wraps into the ghost slot the same way
    public void GetTargetSlot_WithEditModeGhostSlot_WrapsOverTheFullSlotRing(int originalIndex, double rotationOffset, int expectedSlot)
    {
        const double angleStep = 60;
        const int slotCount = 6;

        var slot = PieReorderCalculator.GetTargetSlot(originalIndex, rotationOffset, angleStep, slotCount);

        Assert.Equal(expectedSlot, slot);
    }

    [Theory]
    [InlineData(0, 288, -72)]    // short way is backward
    [InlineData(300, 0, 360)]    // short way keeps going forward
    [InlineData(-10, 0, 0)]      // already closest
    [InlineData(-350, 0, -360)]  // stays near the current winding
    public void GetNearestEquivalentAngle_TakesShortWayAround(double currentAngle, double targetAngle, double expectedAngle)
    {
        var angle = PieReorderCalculator.GetNearestEquivalentAngle(currentAngle, targetAngle);

        Assert.Equal(expectedAngle, angle, precision: 6);
    }

    [Fact]
    public void GetSlotAssignments_DraggedForwardShiftsOthersBack()
    {
        var slots = PieReorderCalculator.GetSlotAssignments(sliceCount: 5, draggedIndex: 0, targetSlot: 2);

        // Original order 0,1,2,3,4 with 0 floating over slot 2 -> 1,2,0,3,4.
        Assert.Equal([2, 0, 1, 3, 4], slots);
    }

    [Fact]
    public void GetSlotAssignments_DraggedBackwardShiftsOthersForward()
    {
        var slots = PieReorderCalculator.GetSlotAssignments(sliceCount: 5, draggedIndex: 4, targetSlot: 0);

        // Original order 0,1,2,3,4 with 4 floating over slot 0 -> 4,0,1,2,3.
        Assert.Equal([1, 2, 3, 4, 0], slots);
    }

    [Fact]
    public void GetSlotAssignments_TargetSlotIsOriginalIndexKeepsOrder()
    {
        var slots = PieReorderCalculator.GetSlotAssignments(sliceCount: 3, draggedIndex: 1, targetSlot: 1);

        Assert.Equal([0, 1, 2], slots);
    }
}
