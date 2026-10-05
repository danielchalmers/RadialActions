using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace RadialActions;

public static class UpdateService
{
    // The same release the Settings banner's Download button opens, so the announced version is always the one users land on.
    private const string GitHubLatestReleaseApiUrl = "https://api.github.com/repos/danielchalmers/RadialActions/releases/latest";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static async Task<Version> GetLatestVersion()
    {
        try
        {
            using var response = await HttpClient.GetAsync(GitHubLatestReleaseApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Update check failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync();
            if (!TryGetLatestReleaseVersion(payload, out var latestVersion))
            {
                Log.Warning("Update check did not return a published stable release with a parseable version and an installer");
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

    /// <summary>
    /// Reads the version of a release from the GitHub latest release payload, if it's ready to install.
    /// </summary>
    /// <remarks>
    /// A release is published before CI attaches its files, so it only counts once an installer is attached; the MSIs upload after the zips, so the zips are there too.
    /// </remarks>
    internal static bool TryGetLatestReleaseVersion(string payload, out Version latestVersion)
    {
        var release = JsonConvert.DeserializeObject<GitHubRelease>(payload);

        latestVersion = release is { Draft: false, Prerelease: false } && HasInstaller(release)
            ? TryParseVersion(release.TagName)
            : null;

        return latestVersion != null;
    }

    private static bool HasInstaller(GitHubRelease release)
    {
        return release.Assets?.Any(asset => asset?.Name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true) == true;
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

        [JsonProperty("draft")]
        public bool Draft { get; init; }

        [JsonProperty("prerelease")]
        public bool Prerelease { get; init; }

        [JsonProperty("assets")]
        public List<GitHubReleaseAsset> Assets { get; init; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonProperty("name")]
        public string Name { get; init; }
    }
}
