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
