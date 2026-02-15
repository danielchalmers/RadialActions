using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace RadialActions;

internal sealed class UpdateCheckService
{
    public const string ReleasesPageUrl = "https://github.com/danielchalmers/RadialActions/releases";
    private const string ReleasesApiUrl = "https://api.github.com/repos/danielchalmers/RadialActions/releases?per_page=32";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly Regex SemVersionRegex = new(
        @"^[vV]?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.\d+)?(?:-(?<prerelease>[0-9A-Za-z\.-]+))?(?:\+[0-9A-Za-z\.-]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _hasChecked;
    private UpdateCheckResult _cachedResult;

    public static UpdateCheckService Instance { get; } = new();

    public event Action<UpdateCheckResult> CheckCompleted;

    public bool HasChecked => _hasChecked;

    public UpdateCheckResult CachedResult =>
        _hasChecked ? _cachedResult : UpdateCheckResult.NotChecked;

    public async Task<UpdateCheckResult> CheckOnceAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        if (_hasChecked)
        {
            return _cachedResult;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_hasChecked)
            {
                return _cachedResult;
            }

            _cachedResult = isEnabled
                ? await CheckForUpdatesAsync(cancellationToken)
                : UpdateCheckResult.Disabled;
            _hasChecked = true;
        }
        finally
        {
            _gate.Release();
        }

        CheckCompleted?.Invoke(_cachedResult);
        return _cachedResult;
    }

    private static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        if (!TryParseSemVersion(GetCurrentVersionText(), out var currentVersion))
        {
            Log.Warning("Skipping update check because current version could not be parsed");
            return UpdateCheckResult.NoUpdate;
        }

        try
        {
            using var response = await HttpClient.GetAsync(ReleasesApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Update check failed with HTTP {StatusCode}", (int)response.StatusCode);
                return UpdateCheckResult.NoUpdate;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var releases = JArray.Parse(json);

            ReleaseVersion? newest = null;
            foreach (var release in releases.OfType<JObject>())
            {
                if (release.Value<bool?>("draft") == true)
                {
                    continue;
                }

                var tagName = release.Value<string>("tag_name");
                if (!TryParseSemVersion(tagName, out var releaseVersion))
                {
                    continue;
                }

                if (releaseVersion.CompareTo(currentVersion) <= 0)
                {
                    continue;
                }

                if (!newest.HasValue || releaseVersion.CompareTo(newest.Value.SemVersion) > 0)
                {
                    newest = new ReleaseVersion(
                        releaseVersion,
                        string.IsNullOrWhiteSpace(tagName) ? releaseVersion.ToString() : tagName,
                        release.Value<string>("html_url") ?? ReleasesPageUrl);
                }
            }

            if (!newest.HasValue)
            {
                return UpdateCheckResult.NoUpdate;
            }

            return new UpdateCheckResult(
                IsEnabled: true,
                IsUpdateAvailable: true,
                LatestVersion: newest.Value.Label,
                ReleaseUrl: newest.Value.Url);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Update check failed");
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
        return versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "0.0.0";
    }

    private static bool TryParseSemVersion(string value, out SemVersion semVersion)
    {
        semVersion = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = SemVersionRegex.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["major"].Value, out var major) ||
            !int.TryParse(match.Groups["minor"].Value, out var minor) ||
            !int.TryParse(match.Groups["patch"].Value, out var patch))
        {
            return false;
        }

        semVersion = new SemVersion(
            major,
            minor,
            patch,
            match.Groups["prerelease"].Value);
        return true;
    }

    private readonly record struct ReleaseVersion(SemVersion SemVersion, string Label, string Url);

    private readonly record struct SemVersion(int Major, int Minor, int Patch, string PreRelease) : IComparable<SemVersion>
    {
        public int CompareTo(SemVersion other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0) return major;

            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0) return minor;

            var patch = Patch.CompareTo(other.Patch);
            if (patch != 0) return patch;

            var thisHasPreRelease = !string.IsNullOrWhiteSpace(PreRelease);
            var otherHasPreRelease = !string.IsNullOrWhiteSpace(other.PreRelease);
            if (!thisHasPreRelease && !otherHasPreRelease) return 0;
            if (!thisHasPreRelease) return 1;
            if (!otherHasPreRelease) return -1;

            return ComparePreRelease(PreRelease, other.PreRelease);
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(PreRelease)
                ? $"{Major}.{Minor}.{Patch}"
                : $"{Major}.{Minor}.{Patch}-{PreRelease}";
        }

        private static int ComparePreRelease(string left, string right)
        {
            var leftParts = left.Split('.');
            var rightParts = right.Split('.');
            var count = Math.Min(leftParts.Length, rightParts.Length);

            for (var i = 0; i < count; i++)
            {
                var leftPart = leftParts[i];
                var rightPart = rightParts[i];

                var leftNumeric = int.TryParse(leftPart, out var leftNumber);
                var rightNumeric = int.TryParse(rightPart, out var rightNumber);

                int comparison;
                if (leftNumeric && rightNumeric)
                {
                    comparison = leftNumber.CompareTo(rightNumber);
                }
                else if (leftNumeric)
                {
                    comparison = -1;
                }
                else if (rightNumeric)
                {
                    comparison = 1;
                }
                else
                {
                    comparison = string.Compare(leftPart, rightPart, StringComparison.OrdinalIgnoreCase);
                }

                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return leftParts.Length.CompareTo(rightParts.Length);
        }
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
