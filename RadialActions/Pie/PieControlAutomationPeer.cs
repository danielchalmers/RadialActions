using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RadialActions;

/// <summary>
/// Exposes the edit-mode pie to UI Automation as a selectable list of actions plus an add button, so screen readers can enumerate and operate the editor. The live menu keeps the default peer behavior.
/// </summary>
internal sealed class PieControlAutomationPeer : UserControlAutomationPeer, ISelectionProvider
{
    public PieControlAutomationPeer(PieControl owner)
        : base(owner)
    {
    }

    private PieControl Pie => (PieControl)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return Pie.IsEditMode ? AutomationControlType.List : base.GetAutomationControlTypeCore();
    }

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }

        return Pie.IsEditMode ? "Actions" : string.Empty;
    }

    protected override List<AutomationPeer> GetChildrenCore()
    {
        if (!Pie.IsEditMode)
        {
            return base.GetChildrenCore();
        }

        var children = new List<AutomationPeer>();
        foreach (var sliceVisual in Pie.SliceVisuals)
        {
            children.Add(new PieSliceAutomationPeer(sliceVisual.Path, Pie, sliceVisual.Action));
        }

        if (Pie.GhostPath != null)
        {
            children.Add(new PieGhostSliceAutomationPeer(Pie.GhostPath, Pie));
        }

        return children;
    }

    public override object GetPattern(PatternInterface patternInterface)
    {
        if (Pie.IsEditMode && patternInterface == PatternInterface.Selection)
        {
            return this;
        }

        return base.GetPattern(patternInterface);
    }

    public bool CanSelectMultiple => false;

    public bool IsSelectionRequired => false;

    public IRawElementProviderSimple[] GetSelection()
    {
        var selected = Pie.SelectedSlice;
        if (selected == null)
        {
            return [];
        }

        var peer = GetChildrenCore()?.OfType<PieSliceAutomationPeer>().FirstOrDefault(child => child.Action == selected);
        return peer == null ? [] : [ProviderFromPeer(peer)];
    }

    public void RaiseSelectionEvent(PieAction selected)
    {
        var peer = GetChildrenCore()?.OfType<PieSliceAutomationPeer>().FirstOrDefault(child => child.Action == selected);
        peer?.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
    }
}

/// <summary>
/// A single action slice exposed as a selectable list item.
/// </summary>
internal sealed class PieSliceAutomationPeer : FrameworkElementAutomationPeer, ISelectionItemProvider
{
    private readonly PieControl _pie;

    public PieSliceAutomationPeer(Path owner, PieControl pie, PieAction action)
        : base(owner)
    {
        _pie = pie;
        Action = action;
    }

    public PieAction Action { get; }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => nameof(PieSliceAutomationPeer);

    protected override string GetNameCore() => Action.Name ?? string.Empty;

    protected override string GetHelpTextCore() => Action.IsEnabled ? Action.Type.ToString() : $"{Action.Type}, hidden from menu";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public override object GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);
    }

    public bool IsSelected => _pie.SelectedSlice == Action;

    public IRawElementProviderSimple SelectionContainer
    {
        get
        {
            var containerPeer = UIElementAutomationPeer.FromElement(_pie);
            return containerPeer == null ? null : ProviderFromPeer(containerPeer);
        }
    }

    public void Select()
    {
        _pie.Dispatcher.InvokeAsync(() => _pie.SelectedSlice = Action, DispatcherPriority.Input);
    }

    public void AddToSelection() => Select();

    public void RemoveFromSelection()
    {
        // Single-selection list; deselecting an item is not supported.
    }
}

/// <summary>
/// The ghost add slice exposed as an invokable button.
/// </summary>
internal sealed class PieGhostSliceAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
    private readonly PieControl _pie;

    public PieGhostSliceAutomationPeer(Path owner, PieControl pie)
        : base(owner)
    {
        _pie = pie;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => nameof(PieGhostSliceAutomationPeer);

    protected override string GetNameCore() => "Add an action";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public override object GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
    }

    public void Invoke()
    {
        _pie.Dispatcher.InvokeAsync(_pie.RequestAddSlice, DispatcherPriority.Input);
    }
}
