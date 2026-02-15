using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace RadialActions;

public sealed class UpdateService : ObservableObject
{
    private const string GitHubReleasesApiUrl = "https://api.github.com/repos/danielchalmers/RadialActions/releases";
    private static readonly Regex VersionPattern = new(@"\d+(?:\.\d+){0,3}", RegexOptions.Compiled);
    private static readonly Uri ReleasesPageUri = new("https://github.com/danielchalmers/RadialActions/releases");
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private bool? _isUpdateAvailable;
    private Version _latestVersion;
    private Uri _releaseUrl = ReleasesPageUri;

    private UpdateService() { }

    public static UpdateService Instance { get; } = new();

    public bool? IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetProperty(ref _isUpdateAvailable, value);
    }

    public Version LatestVersion
    {
        get => _latestVersion;
        private set => SetProperty(ref _latestVersion, value);
    }

    public Uri ReleaseUrl
    {
        get => _releaseUrl;
        private set => SetProperty(ref _releaseUrl, value);
    }

    public async Task CheckAsync(Version currentVersion)
    {
        try
        {
            using var response = await HttpClient.GetAsync(GitHubReleasesApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Update check failed with status code {StatusCode}", response.StatusCode);
                IsUpdateAvailable = null;
                return;
            }

            var payload = await response.Content.ReadAsStringAsync();
            var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(payload);
            if (releases == null || releases.Count == 0)
            {
                Log.Warning("Update check returned no releases");
                IsUpdateAvailable = null;
                return;
            }

            var newestRelease = releases
                .Where(r => !r.Draft)
                .Select(r => new
                {
                    Release = r,
                    ParsedVersion = TryParseVersion(r.TagName) ?? TryParseVersion(r.Name),
                })
                .Where(r => r.ParsedVersion != null)
                .OrderByDescending(r => r.ParsedVersion)
                .FirstOrDefault();

            if (newestRelease == null)
            {
                Log.Warning("Update check did not find any parseable release versions");
                IsUpdateAvailable = null;
                return;
            }

            LatestVersion = newestRelease.ParsedVersion;
            ReleaseUrl = Uri.TryCreate(newestRelease.Release.HtmlUrl, UriKind.Absolute, out var releaseUri)
                ? releaseUri
                : ReleasesPageUri;

            if (currentVersion == null)
            {
                IsUpdateAvailable = null;
                return;
            }

            IsUpdateAvailable = newestRelease.ParsedVersion > currentVersion;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates");
            IsUpdateAvailable = null;
        }
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

    private static Version TryParseVersion(string value)
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
