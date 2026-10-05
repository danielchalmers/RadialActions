using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RadialActions;

public partial class ActionsSettingsView
{
    private const string ListChangeActivityId = "ActionsListChange";

    // DragOver fires many times per second; cache the extracted targets for the current drag so the payload is read once instead of on every tick.
    private IDataObject _dragData;
    private IReadOnlyList<string> _dragTargets = [];

    public ActionsSettingsView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
        ((INotifyCollectionChanged)ActionsList.Items).CollectionChanged += ActionsList_ItemsChanged;
    }

    private ActionsSettingsViewModel ViewModel => DataContext as ActionsSettingsViewModel;

    /// <summary>
    /// Moves keyboard focus to the selected action in the list, or to the list itself when nothing is selected.
    /// </summary>
    public void FocusSelectedAction()
    {
        ScrollSelectionIntoView();

        // Moving an item regenerates its container during the next layout pass, and a running command may still change the selection, so read both after layout has run.
        Dispatcher.InvokeAsync(() =>
        {
            var selected = ActionsList.SelectedItem;
            if (selected != null && ActionsList.ItemContainerGenerator.ContainerFromItem(selected) is ListViewItem container)
            {
                container.Focus();
            }
            else
            {
                ActionsList.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    private void ScrollSelectionIntoView()
    {
        if (ActionsList.SelectedItem != null)
        {
            ActionsList.ScrollIntoView(ActionsList.SelectedItem);
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // The pie's Edit command can select an action before this tab is shown, so bring the selection into view once the list appears.
        if (e.NewValue is true)
        {
            ScrollSelectionIntoView();
        }
    }

    private void ActionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ScrollSelectionIntoView();
    }

    private void ActionsList_ItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Moving the selected action keeps it selected without raising SelectionChanged, so follow it once the list has laid out its new position.
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            Dispatcher.InvokeAsync(ScrollSelectionIntoView, DispatcherPriority.Loaded);
        }
    }

    private void AddActionButton_Click(object sender, RoutedEventArgs e)
    {
        // Click is raised before the button runs its command, so wait for the command and the editor rebinding to finish before focusing the new action's name.
        Dispatcher.InvokeAsync(ActionEditor.FocusName, DispatcherPriority.Input);
    }

    private void MoveButton_Click(object sender, RoutedEventArgs e)
    {
        // Click is raised before the button runs its command, so read the new position once the command has run.
        if (ViewModel is not { SelectedAction: { } action } viewModel)
            return;

        var oldIndex = viewModel.Actions.IndexOf(action);
        Dispatcher.InvokeAsync(() =>
        {
            var newIndex = viewModel.Actions.IndexOf(action);
            if (newIndex >= 0 && newIndex != oldIndex)
            {
                AnnounceListChange(ActionDisplayText.GetMovedAnnouncement(action, newIndex, viewModel.Actions.Count));
            }
        }, DispatcherPriority.Input);
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { SelectedAction: { } action } viewModel)
            return;

        Dispatcher.InvokeAsync(() =>
        {
            if (!viewModel.Actions.Contains(action))
            {
                AnnounceListChange(ActionDisplayText.GetRemovedAnnouncement(action));
            }
        }, DispatcherPriority.Input);
    }

    private void AnnounceListChange(string text)
    {
        // Focus stays on the button, so without this a screen reader says nothing about the result.
        if (!AutomationPeer.ListenerExists(AutomationEvents.Notification))
            return;

        UIElementAutomationPeer.CreatePeerForElement(ActionsList)?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            text,
            ListChangeActivityId);
    }

    private void ActionsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt || ViewModel is not { } viewModel)
            return;

        ICommand command = (e.Key == Key.System ? e.SystemKey : e.Key) switch
        {
            Key.Up => viewModel.MoveUpCommand,
            Key.Down => viewModel.MoveDownCommand,
            _ => null,
        };

        if (command == null)
            return;

        e.Handled = true;
        if (!command.CanExecute(null))
            return;

        command.Execute(null);
        FocusSelectedAction();
    }

    private void ListCommandButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // A focused button that disables itself (Move up at the top, Remove on the last action) would drop keyboard focus to the window; keep it in the list instead.
        if (e.NewValue is false && sender is UIElement { IsKeyboardFocused: true } button)
        {
            // Remove can briefly disable every list command while the selection moves, so re-check after the command finishes; Loaded runs before the keyboard's Input-priority focus re-evaluation.
            Dispatcher.InvokeAsync(() =>
            {
                if (!button.IsEnabled && button.IsKeyboardFocused)
                {
                    FocusSelectedAction();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void ActionsList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDropTargets(e.Data).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void ActionsList_Drop(object sender, DragEventArgs e)
    {
        var targets = GetDropTargets(e.Data);
        ResetDragCache();

        if (targets.Count == 0)
            return;

        ViewModel?.AddDroppedTargets(targets);
        e.Handled = true;
    }

    private IReadOnlyList<string> GetDropTargets(IDataObject data)
    {
        if (ReferenceEquals(data, _dragData))
            return _dragTargets;

        _dragData = data;
        try
        {
            _dragTargets = ActionDropFactory.ExtractTargets(data);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read dropped data");
            _dragTargets = [];
        }

        return _dragTargets;
    }

    private void ResetDragCache()
    {
        _dragData = null;
        _dragTargets = [];
    }
}
