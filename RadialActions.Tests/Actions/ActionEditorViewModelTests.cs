namespace RadialActions.Tests;

public sealed class ActionEditorViewModelTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "RadialActions.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ActionTypes_UseTypeNamesAndAnnounceThemThroughToString()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(["Key", "Open", "Script"], viewModel.ActionTypes.Select(option => option.Name));
        Assert.All(viewModel.ActionTypes, option => Assert.Equal(option.Name, option.ToString()));
    }

    [Fact]
    public void ActionTypes_UseFluentGlyphsInsteadOfEmoji()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(["\uE765", "\uE8A7", "\uE756"], viewModel.ActionTypes.Select(option => option.Glyph));
    }

    [Fact]
    public void KeyActionOptions_EndWithCustomShortcutWithoutEllipsis()
    {
        var viewModel = CreateViewModel();

        var custom = viewModel.KeyActionOptions[^1];
        Assert.Equal(ActionEditorViewModel.CustomKeyActionId, custom.Id);
        Assert.Equal("Custom shortcut", custom.Name);
        Assert.Equal("Custom shortcut", custom.ToString());
    }

    [Fact]
    public void SelectedActionTypeDescription_DescribesSelectedTypeAndUpdatesWithIt()
    {
        var action = new PieAction();
        var viewModel = CreateViewModel(action);
        viewModel.SelectedAction = action;
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal(ActionDisplayText.NotSetUpTypeDescription, viewModel.SelectedActionTypeDescription);

        viewModel.SelectedActionType = ActionType.Open;

        Assert.Equal(ActionDisplayText.GetTypeDescription(ActionType.Open), viewModel.SelectedActionTypeDescription);
        Assert.Contains(nameof(ActionEditorViewModel.SelectedActionTypeDescription), changed);
    }

    [Fact]
    public void ScriptInterpreter_ReadsAndWritesParameterOnlyForScriptActions()
    {
        var script = PieAction.CreateScriptAction("Backup", "Get-Date", interpreter: "powershell.exe");
        var open = PieAction.CreateOpenAction("Docs", "https://example.com");
        var viewModel = CreateViewModel(script, open);

        viewModel.SelectedAction = script;
        Assert.Equal("powershell.exe", viewModel.ScriptInterpreter);
        viewModel.ScriptInterpreter = ActionEditorViewModel.PowerShell7Interpreter;
        Assert.Equal("pwsh.exe", script.Parameter);

        viewModel.SelectedAction = open;
        Assert.Equal(string.Empty, viewModel.ScriptInterpreter);
        viewModel.ScriptInterpreter = "powershell.exe";
        Assert.Equal("https://example.com", open.Parameter);
    }

    [Fact]
    public void ScriptInterpreter_RaisesChangeWhenParameterChanges()
    {
        var script = PieAction.CreateScriptAction("Backup", "Get-Date", interpreter: "powershell.exe");
        var viewModel = CreateViewModel(script);
        viewModel.SelectedAction = script;
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        script.Parameter = @"C:\Tools\pwsh.exe";

        Assert.Contains(nameof(ActionEditorViewModel.ScriptInterpreter), changed);
        Assert.Equal(@"C:\Tools\pwsh.exe", viewModel.ScriptInterpreter);
    }

    [Fact]
    public void ScriptInterpreterOptions_SuggestBothPowerShells()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(["powershell.exe", "pwsh.exe"], viewModel.ScriptInterpreterOptions);
    }

    [Fact]
    public void GetExistingParentFolder_ReturnsFolderOnlyWhenItExists()
    {
        Directory.CreateDirectory(_tempRoot);

        Assert.Equal(_tempRoot, ActionEditorViewModel.GetExistingParentFolder(Path.Combine(_tempRoot, "app.exe")));
        Assert.Null(ActionEditorViewModel.GetExistingParentFolder(Path.Combine(_tempRoot, "Missing", "app.exe")));
        Assert.Null(ActionEditorViewModel.GetExistingParentFolder("notepad.exe"));
        Assert.Null(ActionEditorViewModel.GetExistingParentFolder(string.Empty));
        Assert.Null(ActionEditorViewModel.GetExistingParentFolder(null));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static ActionEditorViewModel CreateViewModel(params PieAction[] actions)
        => new(new ActionDefaultsService(), actions);
}
