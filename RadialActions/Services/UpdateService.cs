namespace RadialActions;

public sealed class UpdateService
{
    private UpdateService() { }

    public static UpdateService Instance { get; } = new();

    public bool? IsUpdateAvailable { get; private set; }

    public Version LatestVersion { get; private set; }

    public Uri ReleaseUrl { get; private set; } =
        new("https://github.com/danielchalmers/RadialActions/releases");

    public Task CheckAsync(Version currentVersion)
    {
        return Task.CompletedTask;
    }
}
