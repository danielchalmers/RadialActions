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
    /// What releasing the held activation hotkey should do.
    /// </summary>
    public enum ReleaseOutcome
    {
        /// <summary>Nothing is targeted and no slice was ever targeted during the hold; the menu stays open for clicking.</summary>
        None,

        /// <summary>Trigger the targeted slice.</summary>
        TriggerSlice,

        /// <summary>A slice was targeted during the hold but the release landed on nothing; dismiss the menu.</summary>
        Dismiss,
    }

    public readonly record struct ReleaseDecision(ReleaseOutcome Outcome, int SliceIndex)
    {
        public static readonly ReleaseDecision None = new(ReleaseOutcome.None, NoSelection);
        public static readonly ReleaseDecision Dismiss = new(ReleaseOutcome.Dismiss, NoSelection);
    }

    /// <summary>
    /// Decides what a hotkey-release flick should do, honoring the active interaction mode so the triggered slice
    /// always matches the one shown highlighted.
    /// </summary>
    /// <param name="wasSliceTargetedDuringHold">
    /// True if a slice was targeted at any point while the hotkey was held. Releasing on nothing then dismisses the
    /// menu as an abandoned flick, while a plain tap that never reached a slice leaves it open for clicking.
    /// </param>
    public static ReleaseDecision GetReleaseDecision(
        bool isDragActive,
        bool isKeyboardMode,
        int selectedIndex,
        int hoveredIndex,
        bool wasSliceTargetedDuringHold)
    {
        if (isDragActive)
        {
            return ReleaseDecision.None;
        }

        var targetIndex = isKeyboardMode ? selectedIndex : hoveredIndex;
        if (targetIndex != NoSelection)
        {
            return new ReleaseDecision(ReleaseOutcome.TriggerSlice, targetIndex);
        }

        // Keyboard mode keeps its selection until the mouse moves, so the only way to reach here with nothing targeted is through the mouse gliding off a slice again.
        return !isKeyboardMode && wasSliceTargetedDuringHold ? ReleaseDecision.Dismiss : ReleaseDecision.None;
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
