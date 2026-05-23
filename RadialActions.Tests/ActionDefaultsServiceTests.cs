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

        service.ApplyKeyDefaults(blankAction, FindKeyAction("Mute"));
        Assert.Equal("🔇", blankAction.Icon);

        service.ApplyKeyDefaults(action, FindKeyAction("Mute"));
        Assert.Equal("🔇", action.Icon);

        action.Icon = "🎵";
        service.ApplyKeyDefaults(action, FindKeyAction("VolumeUp"));
        Assert.Equal("🎵", action.Icon);

        action.Icon = "🔇";
        service.ApplyKeyDefaults(action, FindKeyAction("VolumeUp"));
        Assert.Equal("🔊", action.Icon);
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
        var action = PieAction.CreateShellAction("Manual Name", firstPath, "🛠️", workingDirectory: _tempRoot);

        service.TrackExistingDefaults([action]);

        action.Parameter = secondPath;
        service.ApplyShellDefaults(action, action.Parameter);

        Assert.Equal("Manual Name", action.Name);
        Assert.Equal("🛠️", action.Icon);
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

        Assert.Equal("📁", action.Icon);
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
