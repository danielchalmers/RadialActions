using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace RadialActions;

public static class UpdateService
{
    private const string GitHubReleasesApiUrl = "https://api.github.com/repos/danielchalmers/RadialActions/releases";
    private static readonly Regex VersionPattern = new(@"\d+(?:\.\d+){0,3}", RegexOptions.Compiled);
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
            if (!TryGetLatestReleaseVersion(payload, out var latestVersion, out var hadReleases))
            {
                if (!hadReleases)
                {
                    Log.Warning("Update check returned no releases");
                }
                else
                {
                    Log.Warning("Update check did not find any parseable release versions");
                }

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

    internal static bool TryGetLatestReleaseVersion(string payload, out Version latestVersion, out bool hadReleases)
    {
        latestVersion = null;
        hadReleases = false;

        var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(payload);
        if (releases == null || releases.Count == 0)
        {
            return false;
        }

        hadReleases = true;

        var newestRelease = releases
            .Where(r => !r.Draft)
            .Select(r => TryParseVersion(r.TagName) ?? TryParseVersion(r.Name))
            .Where(v => v != null)
            .OrderByDescending(v => v)
            .FirstOrDefault();

        if (newestRelease == null)
        {
            return false;
        }

        latestVersion = newestRelease;
        return true;
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

        var match = VersionPattern.Match(normalized);
        return match.Success && Version.TryParse(match.Value, out var parsedVersion)
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
