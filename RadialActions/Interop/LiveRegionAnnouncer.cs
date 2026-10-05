using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Threading;

namespace RadialActions;

/// <summary>
/// Tells screen readers that a live region changed once it can actually be read.
/// </summary>
internal static class LiveRegionAnnouncer
{
    /// <summary>
    /// Raises LiveRegionChanged for an element after the pending layout pass, so a screen reader that reads its name in response finds it visible and measured rather than collapsed. Does nothing when no UI Automation client is listening or the element isn't visible by then.
    /// </summary>
    public static void AnnounceAfterLayout(UIElement element)
    {
        if (element == null || !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            return;

        // Loaded priority runs after data binding and layout (Render), so visibility bindings have applied and the element has been measured.
        element.Dispatcher.InvokeAsync(() =>
        {
            if (element.IsVisible)
                UIElementAutomationPeer.CreatePeerForElement(element)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }, DispatcherPriority.Loaded);
    }
}
