using System.Windows.Input;

namespace RadialActions;

internal sealed class PieSelectionController
{
    internal readonly record struct Item(int Index, double MidAngle);

    public const int NoSelection = -1;

    public int SelectedIndex { get; private set; } = NoSelection;

    public void Reset()
    {
        SelectedIndex = NoSelection;
    }

    public void EnsureSelectionIsValid(IReadOnlyList<Item> items)
    {
        if (SelectedIndex == NoSelection)
        {
            return;
        }

        if (items.All(item => item.Index != SelectedIndex))
        {
            SelectedIndex = NoSelection;
        }
    }

    public void HandleArrowKey(Key key, IReadOnlyList<Item> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        if (SelectedIndex == NoSelection)
        {
            SelectedIndex = key switch
            {
                Key.Up => GetIndexClosestToAngle(items, -90),
                Key.Right => GetIndexClosestToAngle(items, 0),
                Key.Down => GetIndexClosestToAngle(items, 90),
                Key.Left => GetIndexClosestToAngle(items, 180),
                _ => NoSelection,
            };
            return;
        }

        var selectedPosition = GetSelectedPosition(items);
        if (selectedPosition < 0)
        {
            SelectedIndex = NoSelection;
            return;
        }

        if (key is Key.Right or Key.Down)
        {
            SelectedIndex = items[(selectedPosition + 1) % items.Count].Index;
        }
        else if (key is Key.Left or Key.Up)
        {
            SelectedIndex = items[(selectedPosition - 1 + items.Count) % items.Count].Index;
        }
    }

    private int GetSelectedPosition(IReadOnlyList<Item> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Index == SelectedIndex)
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetIndexClosestToAngle(IReadOnlyList<Item> items, double targetAngle)
    {
        var bestIndex = items[0].Index;
        var bestDistance = double.MaxValue;

        foreach (var item in items)
        {
            var distance = Math.Abs(PieLayoutCalculator.NormalizeSignedAngle(item.MidAngle - targetAngle));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = item.Index;
            }
        }

        return bestIndex;
    }
}
