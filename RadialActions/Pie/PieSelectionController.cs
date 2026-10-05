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

    /// <summary>
    /// Decides which slice a hotkey-release flick should trigger, honoring the active interaction mode
    /// so the triggered slice always matches the one shown highlighted.
    /// </summary>
    /// <returns>The slice index to trigger, or <see cref="NoSelection"/> to trigger nothing.</returns>
    public static int GetReleaseTriggerIndex(bool isDragActive, bool isKeyboardMode, int selectedIndex, int hoveredIndex)
    {
        if (isDragActive)
        {
            return NoSelection;
        }

        return isKeyboardMode ? selectedIndex : hoveredIndex;
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

    public bool TrySelectDigit(int digit, IReadOnlyList<Item> items)
    {
        // Digits count clockwise from the top; items are provided in that visual order.
        var position = digit - 1;
        if (position < 0 || position >= items.Count)
        {
            return false;
        }

        SelectedIndex = items[position].Index;
        return true;
    }

    /// <summary>
    /// Selects the slice with <paramref name="index"/>, for example when assistive technology moves focus to it.
    /// </summary>
    /// <returns>False if no such slice exists; the selection is left unchanged.</returns>
    public bool TrySelect(int index, IReadOnlyList<Item> items)
    {
        if (items.All(item => item.Index != index))
        {
            return false;
        }

        SelectedIndex = index;
        return true;
    }

    /// <summary>
    /// Moves clockwise like Tab: from no selection to the first slice, and from the last slice back to the first.
    /// </summary>
    public void SelectNext(IReadOnlyList<Item> items)
    {
        MoveBy(1, items);
    }

    /// <summary>
    /// Moves counterclockwise like Shift+Tab: from no selection to the last slice, and from the first slice back to the last.
    /// </summary>
    public void SelectPrevious(IReadOnlyList<Item> items)
    {
        MoveBy(-1, items);
    }

    public void SelectFirst(IReadOnlyList<Item> items)
    {
        SelectedIndex = items.Count > 0 ? items[0].Index : NoSelection;
    }

    public void SelectLast(IReadOnlyList<Item> items)
    {
        SelectedIndex = items.Count > 0 ? items[^1].Index : NoSelection;
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

    private void MoveBy(int offset, IReadOnlyList<Item> items)
    {
        if (items.Count == 0)
        {
            SelectedIndex = NoSelection;
            return;
        }

        var selectedPosition = GetSelectedPosition(items);
        if (selectedPosition < 0)
        {
            SelectedIndex = offset > 0 ? items[0].Index : items[^1].Index;
            return;
        }

        SelectedIndex = items[(selectedPosition + offset + items.Count) % items.Count].Index;
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
