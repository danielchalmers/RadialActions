using System.Windows;

namespace RadialActions;

/// <summary>
/// Pure math for drag-and-drop slice reordering.
/// </summary>
public static class PieReorderCalculator
{
    /// <summary>
    /// Gets the angle of a point relative to the pie center, in degrees, using the same
    /// convention as slice layout (0 = right, increasing clockwise).
    /// </summary>
    public static double GetPointerAngle(Point center, Point position)
    {
        return Math.Atan2(position.Y - center.Y, position.X - center.X) * 180 / Math.PI;
    }

    /// <summary>
    /// Gets the slot a slice lands in after being rotated away from its original position.
    /// </summary>
    public static int GetTargetSlot(int originalIndex, double rotationOffset, double angleStep, int sliceCount)
    {
        var slotDelta = (int)Math.Round(rotationOffset / angleStep);
        return Mod(originalIndex + slotDelta, sliceCount);
    }

    /// <summary>
    /// Gets the angle equivalent to <paramref name="targetAngle"/> (mod 360) that is closest to
    /// <paramref name="currentAngle"/>, so rotation animations take the short way around.
    /// </summary>
    public static double GetNearestEquivalentAngle(double currentAngle, double targetAngle)
    {
        return targetAngle + (360 * Math.Round((currentAngle - targetAngle) / 360));
    }

    /// <summary>
    /// Assigns a slot to each slice while the dragged slice floats over <paramref name="targetSlot"/>.
    /// The remaining slices keep their relative order and shift to fill the gap.
    /// </summary>
    /// <returns>An array mapping each slice's original index to its current slot.</returns>
    public static int[] GetSlotAssignments(int sliceCount, int draggedIndex, int targetSlot)
    {
        var order = new List<int>(sliceCount);
        for (var i = 0; i < sliceCount; i++)
        {
            if (i != draggedIndex)
            {
                order.Add(i);
            }
        }

        order.Insert(targetSlot, draggedIndex);

        var slots = new int[sliceCount];
        for (var slot = 0; slot < order.Count; slot++)
        {
            slots[order[slot]] = slot;
        }

        return slots;
    }

    private static int Mod(int value, int modulus)
    {
        return ((value % modulus) + modulus) % modulus;
    }
}
