using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace RadialActions;

internal sealed class UpdateCheckService
{
    public const string ReleasesPageUrl = "https://github.com/danielchalmers/RadialActions/releases";
    private const string ReleasesApiUrl = "https://api.github.com/repos/danielchalmers/RadialActions/releases?per_page=32";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private bool _hasChecked;
    private UpdateCheckResult _cachedResult = UpdateCheckResult.NotChecked;

    public static UpdateCheckService Instance { get; } = new();

    public event Action<UpdateCheckResult> CheckCompleted;

    public UpdateCheckResult CachedResult => _cachedResult;

    public async Task<UpdateCheckResult> CheckOnceAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        if (_hasChecked)
        {
            return _cachedResult;
        }

        _hasChecked = true;
        _cachedResult = isEnabled
            ? await CheckForUpdatesAsync(cancellationToken)
            : UpdateCheckResult.Disabled;

        CheckCompleted?.Invoke(_cachedResult);
        return _cachedResult;
    }

    private static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(ReleasesApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.NoUpdate;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var latest = JArray.Parse(json)
                .OfType<JObject>()
                .FirstOrDefault(r => r.Value<bool?>("draft") != true);
            if (latest == null)
            {
                return UpdateCheckResult.NoUpdate;
            }

            var latestTag = NormalizeVersionText(latest.Value<string>("tag_name"));
            var currentVersion = NormalizeVersionText(GetCurrentVersionText());
            if (string.IsNullOrWhiteSpace(latestTag))
            {
                return UpdateCheckResult.NoUpdate;
            }

            var hasUpdate = !VersionEquals(latestTag, currentVersion);
            return new UpdateCheckResult(
                IsEnabled: true,
                IsUpdateAvailable: hasUpdate,
                LatestVersion: latest.Value<string>("tag_name") ?? latestTag,
                ReleaseUrl: latest.Value<string>("html_url") ?? ReleasesPageUrl);
        }
        catch
        {
            return UpdateCheckResult.NoUpdate;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RadialActions/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static string GetCurrentVersionText()
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(App.MainFileInfo.FullName);
        return versionInfo.ProductVersion ?? versionInfo.FileVersion ?? string.Empty;
    }

    private static string NormalizeVersionText(string value)
    {
        var trimmed = (value ?? string.Empty).Trim().TrimStart('v', 'V');
        var plusIndex = trimmed.IndexOf('+');
        if (plusIndex >= 0)
        {
            trimmed = trimmed[..plusIndex];
        }

        return trimmed;
    }

    private static bool VersionEquals(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return left.StartsWith(right + ".", StringComparison.OrdinalIgnoreCase) ||
               right.StartsWith(left + ".", StringComparison.OrdinalIgnoreCase);
    }
}

internal readonly record struct UpdateCheckResult(
    bool IsEnabled,
    bool IsUpdateAvailable,
    string LatestVersion,
    string ReleaseUrl)
{
    public static UpdateCheckResult NotChecked { get; } = new(true, false, string.Empty, UpdateCheckService.ReleasesPageUrl);
    public static UpdateCheckResult Disabled { get; } = new(false, false, string.Empty, UpdateCheckService.ReleasesPageUrl);
    public static UpdateCheckResult NoUpdate { get; } = new(true, false, string.Empty, UpdateCheckService.ReleasesPageUrl);
}
