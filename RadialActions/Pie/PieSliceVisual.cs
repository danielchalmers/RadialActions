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

    public PieSelectionController.Item ToSelectionItem()
    {
        return new PieSelectionController.Item(Index, MidAngle);
    }
}
