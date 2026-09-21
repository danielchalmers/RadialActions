using System.Windows.Input;

namespace RadialActions.Tests;

public class PieSelectionControllerTests
{
    private static readonly PieSelectionController.Item[] DirectionalItems =
    [
        new(0, -60),
        new(1, 20),
        new(2, 100),
        new(3, 170),
    ];

    [Theory]
    [InlineData(true, false, 1, 2, PieSelectionController.NoSelection)]
    [InlineData(true, true, 1, 2, PieSelectionController.NoSelection)]
    [InlineData(false, true, 1, 2, 1)]
    [InlineData(false, true, PieSelectionController.NoSelection, 2, PieSelectionController.NoSelection)]
    [InlineData(false, false, 1, 2, 2)]
    [InlineData(false, false, PieSelectionController.NoSelection, PieSelectionController.NoSelection, PieSelectionController.NoSelection)]
    public void GetReleaseDecision_TriggersTheSliceForTheInteractionMode(
        bool isDragActive,
        bool isKeyboardMode,
        int selectedIndex,
        int hoveredIndex,
        int expectedIndex)
    {
        var result = PieSelectionController.GetReleaseDecision(isDragActive, isKeyboardMode, selectedIndex, hoveredIndex, wasSliceTargetedDuringHold: false);

        var expectedOutcome = expectedIndex == PieSelectionController.NoSelection
            ? PieSelectionController.ReleaseOutcome.None
            : PieSelectionController.ReleaseOutcome.TriggerSlice;
        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedIndex, result.SliceIndex);
    }

    [Fact]
    public void GetReleaseDecision_DismissesWhenTheMouseLeftASliceDuringTheHold()
    {
        var result = PieSelectionController.GetReleaseDecision(
            isDragActive: false,
            isKeyboardMode: false,
            selectedIndex: PieSelectionController.NoSelection,
            hoveredIndex: PieSelectionController.NoSelection,
            wasSliceTargetedDuringHold: true);

        Assert.Equal(PieSelectionController.ReleaseOutcome.Dismiss, result.Outcome);
    }

    [Fact]
    public void GetReleaseDecision_StaysOpenForATapThatNeverReachedASlice()
    {
        var result = PieSelectionController.GetReleaseDecision(
            isDragActive: false,
            isKeyboardMode: false,
            selectedIndex: PieSelectionController.NoSelection,
            hoveredIndex: PieSelectionController.NoSelection,
            wasSliceTargetedDuringHold: false);

        Assert.Equal(PieSelectionController.ReleaseOutcome.None, result.Outcome);
    }

    [Fact]
    public void GetReleaseDecision_HoveredSliceWinsOverAnAbandonedFlick()
    {
        var result = PieSelectionController.GetReleaseDecision(
            isDragActive: false,
            isKeyboardMode: false,
            selectedIndex: PieSelectionController.NoSelection,
            hoveredIndex: 3,
            wasSliceTargetedDuringHold: true);

        Assert.Equal(PieSelectionController.ReleaseOutcome.TriggerSlice, result.Outcome);
        Assert.Equal(3, result.SliceIndex);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GetReleaseDecision_NeverDismissesDuringADragOrInKeyboardMode(bool isDragActive, bool isKeyboardMode)
    {
        var result = PieSelectionController.GetReleaseDecision(
            isDragActive,
            isKeyboardMode,
            selectedIndex: PieSelectionController.NoSelection,
            hoveredIndex: PieSelectionController.NoSelection,
            wasSliceTargetedDuringHold: true);

        Assert.Equal(PieSelectionController.ReleaseOutcome.None, result.Outcome);
    }

    [Theory]
    [InlineData(Key.Up, 0)]
    [InlineData(Key.Right, 1)]
    [InlineData(Key.Down, 2)]
    [InlineData(Key.Left, 3)]
    public void HandleArrowKey_FirstSelectionChoosesClosestSlice(Key key, int expectedIndex)
    {
        var controller = new PieSelectionController();

        controller.HandleArrowKey(key, DirectionalItems);

        Assert.Equal(expectedIndex, controller.SelectedIndex);
    }

    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Up)]
    public void HandleArrowKey_LeftAndUpWrapBackward(Key key)
    {
        var controller = new PieSelectionController();
        PieSelectionController.Item[] items =
        [
            new(0, -10),
            new(1, 100),
            new(2, 170),
        ];

        controller.HandleArrowKey(Key.Right, items);
        controller.HandleArrowKey(key, items);

        Assert.Equal(2, controller.SelectedIndex);
    }

    [Theory]
    [InlineData(Key.Right)]
    [InlineData(Key.Down)]
    public void HandleArrowKey_RightAndDownWrapForward(Key key)
    {
        var controller = new PieSelectionController();
        PieSelectionController.Item[] items =
        [
            new(0, -10),
            new(1, 100),
            new(2, 170),
        ];

        controller.HandleArrowKey(Key.Left, items);
        controller.HandleArrowKey(key, items);

        Assert.Equal(0, controller.SelectedIndex);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 5)]
    public void TrySelectDigit_SelectsItemAtClockwisePosition(int digit, int expectedIndex)
    {
        var controller = new PieSelectionController();
        PieSelectionController.Item[] items =
        [
            new(0, -60),
            new(2, 60),
            new(5, 180),
        ];

        var selected = controller.TrySelectDigit(digit, items);

        Assert.True(selected);
        Assert.Equal(expectedIndex, controller.SelectedIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(9)]
    public void TrySelectDigit_ReturnsFalseWhenDigitHasNoSlice(int digit)
    {
        var controller = new PieSelectionController();
        PieSelectionController.Item[] items =
        [
            new(0, -60),
            new(1, 60),
            new(2, 180),
        ];

        var selected = controller.TrySelectDigit(digit, items);

        Assert.False(selected);
        Assert.Equal(PieSelectionController.NoSelection, controller.SelectedIndex);
    }

    [Fact]
    public void EnsureSelectionIsValid_ResetsWhenSelectedIndexIsMissing()
    {
        var controller = new PieSelectionController();
        PieSelectionController.Item[] originalItems =
        [
            new(0, -10),
            new(1, 100),
            new(2, 170),
        ];
        PieSelectionController.Item[] updatedItems =
        [
            new(0, -10),
            new(2, 170),
        ];

        controller.HandleArrowKey(Key.Down, originalItems);
        controller.EnsureSelectionIsValid(updatedItems);

        Assert.Equal(PieSelectionController.NoSelection, controller.SelectedIndex);
    }
}
