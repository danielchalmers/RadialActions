using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace RadialActions;

public static class UpdateService
{
    private const string GitHubReleasesApiUrl = "https://api.github.com/repos/danielchalmers/RadialActions/releases";
    internal static readonly Uri LatestReleasePageUrl = new("https://github.com/danielchalmers/RadialActions/releases/latest");
    private static readonly HttpClient HttpClient = CreateHttpClient();

    internal static async Task<ReleaseInfo> GetLatestRelease()
    {
        try
        {
            using var response = await HttpClient.GetAsync(GitHubReleasesApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Update check failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync();
            if (!TryGetLatestRelease(payload, out var latestRelease))
            {
                Log.Warning("Update check did not return a parseable latest non-draft release");
                return null;
            }

            return latestRelease;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates");
            return null;
        }
    }

    public static async Task<Version> GetLatestVersion()
    {
        var latestRelease = await GetLatestRelease();
        return latestRelease?.Version;
    }

    public static bool IsUpdateAvailable(Version currentVersion, Version latestVersion)
    {
        if (currentVersion == null || latestVersion == null)
        {
            return false;
        }

        return latestVersion > currentVersion;
    }

    internal static bool TryGetLatestReleaseVersion(string payload, out Version latestVersion)
    {
        if (!TryGetLatestRelease(payload, out var latestRelease))
        {
            latestVersion = null;
            return false;
        }

        latestVersion = latestRelease.Version;
        return true;
    }

    internal static bool TryGetLatestRelease(string payload, out ReleaseInfo latestRelease)
    {
        var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(payload);
        if (releases == null)
        {
            latestRelease = null;
            return false;
        }

        latestRelease = releases
            .Where(release => !release.Draft)
            .Select(CreateReleaseInfo)
            .Where(release => release != null)
            .OrderByDescending(release => release.Version)
            .FirstOrDefault();

        return latestRelease != null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("RadialActions");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    internal static Version TryParseVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        return Version.TryParse(normalized, out var parsedVersion)
            ? parsedVersion
            : null;
    }

    private static ReleaseInfo CreateReleaseInfo(GitHubRelease release)
    {
        var version = TryParseVersion(release.TagName);
        if (version == null)
        {
            return null;
        }

        return new ReleaseInfo(version, TryParseReleaseUrl(release.HtmlUrl) ?? LatestReleasePageUrl);
    }

    private static Uri TryParseReleaseUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var parsedUri)
            ? parsedUri
            : null;
    }

    /// <summary>
    /// Information about the latest stable GitHub release.
    /// </summary>
    /// <param name="Version">The parsed release version.</param>
    /// <param name="Url">The release page URL to open for the update.</param>
    internal sealed record ReleaseInfo(Version Version, Uri Url);

    private sealed class GitHubRelease
    {
        [JsonProperty("tag_name")]
        public string TagName { get; init; }

        [JsonProperty("name")]
        public string Name { get; init; }

        [JsonProperty("html_url")]
        public string HtmlUrl { get; init; }

        [JsonProperty("draft")]
        public bool Draft { get; init; }
    }
}
