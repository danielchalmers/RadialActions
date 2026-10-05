namespace RadialActions;

public partial class ActionEditorView
{
    public ActionEditorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Moves keyboard focus to the Name box and selects its text, so a new action can be named right away.
    /// </summary>
    public void FocusName()
    {
        if (!NameBox.Focus())
            return;

        NameBox.SelectAll();
    }
}
