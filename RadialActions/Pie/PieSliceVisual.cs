using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RadialActions;

internal sealed class PieSliceVisual
{
    public required int Index { get; init; }
    public required double MidAngle { get; init; }
    public required PieAction Action { get; init; }
    public required Path Path { get; init; }
    public required SolidColorBrush FillBrush { get; init; }
    public required SolidColorBrush StrokeBrush { get; init; }
    public required ContextMenu ContextMenu { get; init; }

    /// <summary>
    /// Rotates the slice path around the pie center during drag reordering.
    /// </summary>
    public RotateTransform PathRotation { get; set; }

    /// <summary>
    /// Orbits the content panel around the pie center during drag reordering.
    /// </summary>
    public RotateTransform ContentOrbit { get; set; }

    /// <summary>
    /// Counter-rotates the content panel about its own center so it stays upright while orbiting.
    /// </summary>
    public RotateTransform ContentCounter { get; set; }

    public StackPanel ContentPanel { get; set; }

    /// <summary>
    /// The slot this slice currently occupies; starts equal to <see cref="Index"/>.
    /// </summary>
    public int CurrentSlot { get; set; }

    /// <summary>
    /// The rotation in degrees this slice has settled at (or is animating toward) relative to its built position.
    /// </summary>
    public double RotationOffset { get; set; }

    public PieSelectionController.Item ToSelectionItem()
    {
        return new PieSelectionController.Item(Index, MidAngle);
    }
}
