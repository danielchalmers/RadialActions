using System.Windows;

namespace RadialActions;

public partial class ActionsSettingsView
{
    // DragOver fires many times per second; cache the extracted targets for the current drag so the payload is read once instead of on every tick.
    private IDataObject _dragData;
    private IReadOnlyList<string> _dragTargets = [];

    public ActionsSettingsView()
    {
        InitializeComponent();
    }

    private ActionsSettingsViewModel ViewModel => DataContext as ActionsSettingsViewModel;

    private void EditorPie_AddSliceRequested(object sender, EventArgs e)
    {
        ViewModel?.AddActionCommand.Execute(null);
    }

    private void EditorPie_RemoveSliceRequested(object sender, SliceClickEventArgs e)
    {
        ViewModel?.RemoveActionCommand.Execute(null);
    }

    private void PieStage_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDropTargets(e.Data).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void PieStage_Drop(object sender, DragEventArgs e)
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
