namespace RadialActions.Tests;

public sealed class ActionDefaultsServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "RadialActions.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ApplyKeyDefaults_UpdatesIconOnlyForBlankDefaultOrPreviousAutoValue()
    {
        var service = new ActionDefaultsService();
        var blankAction = new PieAction("Blank") { Type = ActionType.Key, Parameter = "Mute", Icon = string.Empty };
        var action = new PieAction("Volume") { Type = ActionType.Key, Parameter = "Mute" };
        var autoAction = new PieAction("Auto") { Type = ActionType.Key, Parameter = "Mute" };
        var mute = FindKeyAction("Mute");
        var volumeUp = FindKeyAction("VolumeUp");

        service.ApplyKeyDefaults(blankAction, mute);
        Assert.Equal(mute.Icon, blankAction.Icon);

        service.ApplyKeyDefaults(action, mute);
        Assert.Equal(mute.Icon, action.Icon);

        action.Icon = "\U0001F3B5";
        service.ApplyKeyDefaults(action, volumeUp);
        Assert.Equal("\U0001F3B5", action.Icon);

        service.ApplyKeyDefaults(autoAction, mute);
        autoAction.Icon = mute.Icon;
        service.ApplyKeyDefaults(autoAction, volumeUp);
        Assert.Equal(volumeUp.Icon, autoAction.Icon);
    }

    [Fact]
    public void ApplySystemDefaults_UpdatesNameAndIconOnlyForBlankDefaultOrPreviousAutoValue()
    {
        var service = new ActionDefaultsService();
        var defaultAction = new PieAction { Type = ActionType.System, Parameter = "LockWorkstation" };
        var manualAction = new PieAction("My Lock") { Type = ActionType.System, Parameter = "LockWorkstation", Icon = "\U0001F3B5" };
        var autoAction = new PieAction { Type = ActionType.System, Parameter = "LockWorkstation" };
        var lockAction = FindSystemAction("LockWorkstation");
        var sleepAction = FindSystemAction("Sleep");

        service.ApplySystemDefaults(defaultAction, lockAction);
        Assert.Equal(lockAction.Name, defaultAction.Name);
        Assert.Equal(lockAction.Icon, defaultAction.Icon);

        service.ApplySystemDefaults(manualAction, lockAction);
        Assert.Equal("My Lock", manualAction.Name);
        Assert.Equal("\U0001F3B5", manualAction.Icon);

        service.ApplySystemDefaults(autoAction, lockAction);
        service.ApplySystemDefaults(autoAction, sleepAction);
        Assert.Equal(sleepAction.Name, autoAction.Name);
        Assert.Equal(sleepAction.Icon, autoAction.Icon);
    }

    [Fact]
    public void EnsureSystemDefaults_UnknownParameter_ResetsToFirstKnownAction()
    {
        var service = new ActionDefaultsService();
        var action = new PieAction { Type = ActionType.System, Parameter = "not-a-real-id" };

        service.EnsureSystemDefaults(action);

        Assert.Equal(PieAction.SystemActions[0].Id, action.Parameter);
        Assert.Equal(PieAction.SystemActions[0].Name, action.Name);
        Assert.Equal(PieAction.SystemActions[0].Icon, action.Icon);
    }

    [Fact]
    public void ApplyShellDefaults_PreservesManualNameIconAndWorkingDirectory()
    {
        Directory.CreateDirectory(_tempRoot);
        var firstRoot = Path.Combine(_tempRoot, "First");
        var secondRoot = Path.Combine(_tempRoot, "Second");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        var firstPath = Path.Combine(firstRoot, "First.exe");
        var secondPath = Path.Combine(secondRoot, "Second.exe");

        var service = new ActionDefaultsService();
        var action = PieAction.CreateShellAction("Manual Name", firstPath, "\U0001F6E0\uFE0F", workingDirectory: _tempRoot);

        service.TrackExistingDefaults([action]);

        action.Parameter = secondPath;
        service.ApplyShellDefaults(action, action.Parameter);

        Assert.Equal("Manual Name", action.Name);
        Assert.Equal("\U0001F6E0\uFE0F", action.Icon);
        Assert.Equal(_tempRoot, action.WorkingDirectory);
    }

    [Fact]
    public void ApplyShellDefaults_LegacyStarIcon_UpgradesToSelectedDefaultIcon()
    {
        Directory.CreateDirectory(_tempRoot);
        var targetPath = Path.Combine(_tempRoot, "First.exe");
        var service = new ActionDefaultsService();
        var action = PieAction.CreateShellAction(PieAction.DefaultName, targetPath, ActionDefaultsService.LegacyDefaultIcon);

        service.ApplyShellDefaults(action, action.Parameter);

        Assert.Equal(ShellActionDefaults.FileIcon, action.Icon);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static KeyActionDefinition FindKeyAction(string id)
    {
        Assert.True(PieAction.TryGetKeyAction(id, out var definition));
        return definition;
    }

    private static SystemActionDefinition FindSystemAction(string id)
    {
        Assert.True(PieAction.TryGetSystemAction(id, out var definition));
        return definition;
    }
}
