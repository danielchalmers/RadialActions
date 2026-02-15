namespace RadialActions;

public sealed class UpdateService
{
    private bool _hasChecked;

    private UpdateService() { }

    public static UpdateService Instance { get; } = new();

    public bool IsUpdateAvailable { get; private set; }

    public Version LatestVersion { get; private set; } = new(0, 0, 0, 0);

    public Uri ReleaseUrl { get; private set; } =
        new("https://github.com/danielchalmers/RadialActions/releases");

    public Task CheckOnceAsync(Version currentVersion)
    {
        if (_hasChecked)
        {
            return Task.CompletedTask;
        }

        _hasChecked = true;
        LatestVersion = currentVersion ?? LatestVersion;
        IsUpdateAvailable = false;
        return Task.CompletedTask;
    }
}
