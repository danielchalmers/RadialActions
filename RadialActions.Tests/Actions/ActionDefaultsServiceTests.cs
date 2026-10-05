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
    public void ApplyOpenDefaults_PreservesManualNameIconAndWorkingDirectory()
    {
        Directory.CreateDirectory(_tempRoot);
        var firstRoot = Path.Combine(_tempRoot, "First");
        var secondRoot = Path.Combine(_tempRoot, "Second");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        var firstPath = Path.Combine(firstRoot, "First.exe");
        var secondPath = Path.Combine(secondRoot, "Second.exe");

        var service = new ActionDefaultsService();
        var action = PieAction.CreateOpenAction("Manual Name", firstPath, "\U0001F6E0\uFE0F", workingDirectory: _tempRoot);

        service.TrackExistingDefaults([action]);

        action.Parameter = secondPath;
        service.ApplyOpenDefaults(action, action.Parameter);

        Assert.Equal("Manual Name", action.Name);
        Assert.Equal("\U0001F6E0\uFE0F", action.Icon);
        Assert.Equal(_tempRoot, action.WorkingDirectory);
    }

    [Fact]
    public void ApplyOpenDefaults_LegacyStarIcon_UpgradesToSelectedDefaultIcon()
    {
        Directory.CreateDirectory(_tempRoot);
        var targetPath = Path.Combine(_tempRoot, "First.exe");
        var service = new ActionDefaultsService();
        var action = PieAction.CreateOpenAction(PieAction.DefaultName, targetPath, ActionDefaultsService.LegacyDefaultIcon);

        service.ApplyOpenDefaults(action, action.Parameter);

        Assert.Equal(OpenActionDefaults.FileIcon, action.Icon);
    }

    [Theory]
    [InlineData(PieAction.DefaultName)]
    [InlineData(ActionDefaultsService.LegacyDefaultName)]
    [InlineData(ActionDefaultsService.LegacyBlankActionName)]
    [InlineData("")]
    public void ApplyKeyDefaults_UntouchedDefaultName_TakesKeyName(string name)
    {
        var service = new ActionDefaultsService();
        var action = new PieAction(name) { Type = ActionType.Key, Parameter = "Mute" };

        service.ApplyKeyDefaults(action, FindKeyAction("Mute"));

        Assert.Equal("Mute", action.Name);
    }

    [Fact]
    public void ApplyKeyDefaults_FollowsPreviousAutoNameButKeepsCustomName()
    {
        var service = new ActionDefaultsService();
        var action = new PieAction { Type = ActionType.Key, Parameter = "Mute" };

        service.ApplyKeyDefaults(action, FindKeyAction("Mute"));
        service.ApplyKeyDefaults(action, FindKeyAction("VolumeUp"));
        Assert.Equal("Volume Up", action.Name);

        action.Name = "Louder";
        service.ApplyKeyDefaults(action, FindKeyAction("VolumeDown"));
        Assert.Equal("Louder", action.Name);
    }

    [Theory]
    [InlineData(PieAction.DefaultName)]
    [InlineData(ActionDefaultsService.LegacyDefaultName)]
    [InlineData(ActionDefaultsService.LegacyBlankActionName)]
    public void ApplyOpenDefaults_UntouchedDefaultName_TakesTargetName(string name)
    {
        var service = new ActionDefaultsService();
        var action = PieAction.CreateOpenAction(name, "https://example.com", PieAction.DefaultIcon);

        service.ApplyOpenDefaults(action, action.Parameter);

        Assert.Equal("example.com", action.Name);
        Assert.Equal(OpenActionDefaults.WebIcon, action.Icon);
    }

    [Fact]
    public void ApplyOpenDefaults_AfterKeyAutoValues_ReplacesThem()
    {
        var service = new ActionDefaultsService();
        var action = new PieAction { Type = ActionType.Key, Parameter = "Mute" };
        service.ApplyKeyDefaults(action, FindKeyAction("Mute"));

        action.Type = ActionType.Open;
        action.Parameter = "https://example.com";
        service.ApplyOpenDefaults(action, action.Parameter);

        Assert.Equal("example.com", action.Name);
        Assert.Equal(OpenActionDefaults.WebIcon, action.Icon);
    }

    [Fact]
    public void ApplyKeyDefaults_AfterOpenAutoValues_ReplacesThem()
    {
        var service = new ActionDefaultsService();
        var action = PieAction.CreateOpenAction(PieAction.DefaultName, "https://example.com", PieAction.DefaultIcon);
        service.ApplyOpenDefaults(action, action.Parameter);

        action.Type = ActionType.Key;
        action.Parameter = "Mute";
        service.ApplyKeyDefaults(action, FindKeyAction("Mute"));

        Assert.Equal("Mute", action.Name);
        Assert.Equal(FindKeyAction("Mute").Icon, action.Icon);
    }

    [Fact]
    public void TrackExistingDefaults_KeyActionWithCustomName_KeepsNameWhenKeyChanges()
    {
        var service = new ActionDefaultsService();
        var action = new PieAction("Silence") { Type = ActionType.Key, Parameter = "Mute" };
        service.TrackExistingDefaults([action]);

        service.ApplyKeyDefaults(action, FindKeyAction("VolumeDown"));

        Assert.Equal("Silence", action.Name);
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
