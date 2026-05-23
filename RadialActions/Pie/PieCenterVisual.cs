using System.Windows.Controls;
using System.Windows.Media;

namespace RadialActions;

internal sealed class PieCenterVisual
{
    public required Grid Target { get; init; }
    public required SolidColorBrush FillBrush { get; init; }
    public required SolidColorBrush StrokeBrush { get; init; }
    public required TextBlock Icon { get; init; }
}
