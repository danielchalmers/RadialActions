using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace RadialActions;

public static class UpdateService
{
    private const string GitHubReleasesApiUrl = "https://api.github.com/repos/danielchalmers/RadialActions/releases";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static async Task<Version> GetLatestVersion()
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
            if (!TryGetLatestReleaseVersion(payload, out var latestVersion))
            {
                Log.Warning("Update check did not return a parseable latest non-draft release version");
                return null;
            }

            return latestVersion;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates");
            return null;
        }
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
        var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(payload);
        if (releases == null)
        {
            latestVersion = null;
            return false;
        }

        latestVersion = releases
            .Where(release => !release.Draft)
            .Select(release => TryParseVersion(release.TagName))
            .Where(version => version != null)
            .OrderByDescending(version => version)
            .FirstOrDefault();

        return latestVersion != null;
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
