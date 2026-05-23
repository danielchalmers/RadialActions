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
}
