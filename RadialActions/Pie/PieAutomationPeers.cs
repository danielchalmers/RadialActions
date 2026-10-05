using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace RadialActions;

/// <summary>
/// Exposes the pie to UI Automation as a menu of named menu items, so screen readers can announce, count, focus and invoke slices.
/// </summary>
/// <remarks>
/// Slices are Paths, which have no automation peers of their own, so this peer wraps each current slice visual in a <see cref="PieSliceAutomationPeer"/>.
/// Peers only read the visuals PieControl already built: they never trigger a build, and nothing runs an action except an explicit Invoke.
/// </remarks>
internal sealed class PieControlAutomationPeer : FrameworkElementAutomationPeer
{
    private const string SelectionActivityId = "PieSelection";

    private Dictionary<PieSliceVisual, PieSliceAutomationPeer> _slicePeers = [];
    private PieCenterAutomationPeer _centerPeer;

    public PieControlAutomationPeer(PieControl owner)
        : base(owner)
    {
    }

    private PieControl Pie => (PieControl)Owner;

    /// <summary>
    /// Announces the keyboard-selected slice: a focus change on its peer, plus a notification with its name and position as a fallback for screen readers that ignore focus on wrapper peers.
    /// </summary>
    public void AnnounceSelection(PieSliceVisual sliceVisual)
    {
        RaiseSliceFocusChanged(sliceVisual);

        if (ListenerExists(AutomationEvents.Notification))
        {
            RaiseNotificationEvent(
                AutomationNotificationKind.Other,
                AutomationNotificationProcessing.ImportantMostRecent,
                PieAccessibilityText.DescribeSelection(sliceVisual.Action, sliceVisual.Index, Pie.SliceVisuals.Count),
                SelectionActivityId);
        }
    }

    public void RaiseSliceFocusChanged(PieSliceVisual sliceVisual)
    {
        if (!ListenerExists(AutomationEvents.AutomationFocusChanged))
        {
            return;
        }

        // Realizes the children so the slice peer is parented before it raises an event.
        GetChildren();
        GetSlicePeer(sliceVisual).RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;

    protected override string GetClassNameCore() => nameof(PieControl);

    protected override List<AutomationPeer> GetChildrenCore()
    {
        var sliceVisuals = Pie.SliceVisuals;
        var children = new List<AutomationPeer>(sliceVisuals.Count + 1);

        // Keep peers for visuals that survived, and let peers of visuals replaced by a rebuild go.
        var previousPeers = _slicePeers;
        _slicePeers = new Dictionary<PieSliceVisual, PieSliceAutomationPeer>(sliceVisuals.Count);
        foreach (var sliceVisual in sliceVisuals)
        {
            if (!previousPeers.TryGetValue(sliceVisual, out var slicePeer))
            {
                slicePeer = new PieSliceAutomationPeer(Pie, sliceVisual);
            }

            _slicePeers[sliceVisual] = slicePeer;
            children.Add(slicePeer);
        }

        var centerVisual = Pie.CenterVisual;
        if (centerVisual == null)
        {
            _centerPeer = null;
        }
        else
        {
            if (_centerPeer?.Owner != centerVisual.Target)
            {
                _centerPeer = new PieCenterAutomationPeer(Pie, centerVisual.Target);
            }

            children.Add(_centerPeer);
        }

        return children;
    }

    private PieSliceAutomationPeer GetSlicePeer(PieSliceVisual sliceVisual)
    {
        if (!_slicePeers.TryGetValue(sliceVisual, out var slicePeer))
        {
            slicePeer = new PieSliceAutomationPeer(Pie, sliceVisual);
            _slicePeers[sliceVisual] = slicePeer;
        }

        return slicePeer;
    }
}

/// <summary>
/// One slice as a menu item: named after its action, numbered in clockwise order, focusable and invokable.
/// </summary>
internal sealed class PieSliceAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
    private readonly PieControl _pie;
    private readonly PieSliceVisual _slice;

    public PieSliceAutomationPeer(PieControl pie, PieSliceVisual slice)
        : base(slice.Path)
    {
        _pie = pie;
        _slice = slice;
    }

    public override object GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
    }

    /// <summary>
    /// Runs the slice exactly like pressing Enter on it. This is the only path from automation to an action.
    /// </summary>
    public void Invoke()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        _pie.InvokeSliceFromAutomation(_slice);
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

    protected override string GetClassNameCore() => "PieSlice";

    protected override string GetNameCore() => PieAccessibilityText.GetSliceName(_slice.Action);

    // FrameworkElementAutomationPeer falls back to the ToolTip, which only repeats the name for trimmed labels.
    protected override string GetHelpTextCore() => string.Empty;

    protected override string GetAccessKeyCore() => PieAccessibilityText.GetAccessKey(_slice.Index);

    protected override int GetPositionInSetCore() => _slice.Index + 1;

    protected override int GetSizeOfSetCore() => _pie.SliceVisuals.Count;

    protected override List<AutomationPeer> GetChildrenCore() => null;

    protected override bool IsEnabledCore() => _pie.CanInvokeFromAutomation;

    protected override bool IsKeyboardFocusableCore() => true;

    protected override bool HasKeyboardFocusCore() => _pie.IsSliceKeyboardFocused(_slice);

    protected override void SetFocusCore() => _pie.SelectSliceForAutomation(_slice);

    // The center of a wedge's bounding box can fall on the hub or a neighbor, so clicks target the slice's content instead.
    protected override Point GetClickablePointCore()
    {
        return PresentationSource.FromVisual(_slice.Path) == null
            ? new Point(double.NaN, double.NaN)
            : _slice.Path.PointToScreen(_slice.ContentCenter);
    }

    // A Path in a Canvas lays out from the canvas origin, so its layout box is far larger than the wedge; use the geometry instead.
    protected override Rect GetBoundingRectangleCore()
    {
        var path = _slice.Path;
        if (PresentationSource.FromVisual(path) == null || path.Data == null)
        {
            return Rect.Empty;
        }

        var bounds = path.Data.Bounds;
        var screenBounds = new Rect(path.PointToScreen(bounds.TopLeft), path.PointToScreen(bounds.BottomRight));
        screenBounds.Union(path.PointToScreen(bounds.TopRight));
        screenBounds.Union(path.PointToScreen(bounds.BottomLeft));
        return screenBounds;
    }

    // The base hit test (ElementFromPoint) uses the Path's layout box, which spans most of the pie, so test the wedge itself; PointFromScreen undoes the press scale and drag rotation.
    protected override AutomationPeer GetPeerFromPointCore(Point point)
    {
        var path = _slice.Path;
        if (path.Data == null || PresentationSource.FromVisual(path) == null)
        {
            return null;
        }

        return path.Data.FillContains(path.PointFromScreen(point)) ? this : null;
    }
}

/// <summary>
/// The hub as a "Close menu" button.
/// </summary>
internal sealed class PieCenterAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
    private readonly PieControl _pie;

    public PieCenterAutomationPeer(PieControl pie, FrameworkElement target)
        : base(target)
    {
        _pie = pie;
    }

    public override object GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
    }

    public void Invoke()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        _pie.InvokeCenterFromAutomation();
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => "PieCenter";

    protected override string GetNameCore() => PieAccessibilityText.CloseMenuName;

    protected override string GetAcceleratorKeyCore() => "Esc";

    protected override List<AutomationPeer> GetChildrenCore() => null;

    protected override bool IsEnabledCore() => _pie.CanInvokeFromAutomation;

    protected override bool IsKeyboardFocusableCore() => false;

    // The hub is checked before the slices, so only claim points inside its circle, not the corners of its square layout box.
    protected override AutomationPeer GetPeerFromPointCore(Point point)
    {
        var target = (FrameworkElement)Owner;
        if (PresentationSource.FromVisual(target) == null)
        {
            return null;
        }

        var radius = target.ActualWidth / 2;
        var center = new Point(radius, target.ActualHeight / 2);
        return (target.PointFromScreen(point) - center).Length <= radius ? this : null;
    }
}
