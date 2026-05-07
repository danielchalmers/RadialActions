namespace RadialActions.Tests;

public class UpdateServiceTests
{
    [Fact]
    public void IsUpdateAvailable_WhenLatestGreater_ReturnsTrue()
    {
        var isAvailable = UpdateService.IsUpdateAvailable(new Version(1, 2, 0), new Version(1, 3, 0));

        Assert.True(isAvailable);
    }

    [Theory]
    [InlineData("1.3.0", "1.3.0")]
    [InlineData("1.3.1", "1.3.0")]
    public void IsUpdateAvailable_WhenLatestNotGreater_ReturnsFalse(string current, string latest)
    {
        var isAvailable = UpdateService.IsUpdateAvailable(Version.Parse(current), Version.Parse(latest));

        Assert.False(isAvailable);
    }

    [Fact]
    public void IsUpdateAvailable_WhenCurrentVersionMissing_ReturnsFalse()
    {
        var isAvailable = UpdateService.IsUpdateAvailable(null, new Version(1, 0, 0));

        Assert.False(isAvailable);
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V2.0", "2.0")]
    [InlineData("v1.2.3-preview1", "1.2.3")]
    [InlineData("  v3.0.1  ", "3.0.1")]
    public void TryParseVersion_ValidFormats_ReturnsVersion(string raw, string expected)
    {
        var parsed = UpdateService.TryParseVersion(raw);

        Assert.Equal(Version.Parse(expected), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("release-10.4.7-beta1")]
    [InlineData("no-version-here")]
    public void TryParseVersion_InvalidFormats_ReturnsNull(string raw)
    {
        var parsed = UpdateService.TryParseVersion(raw);

        Assert.Null(parsed);
    }

    [Fact]
    public void TryGetLatestReleaseVersion_UsesFirstNonDraftRelease()
    {
        const string payload = """
        [
          { "tag_name": "v9.9.9", "name": "v9.9.9", "draft": true },
          { "tag_name": "v1.2.3-preview1", "name": "v1.2.3-preview1", "draft": false },
          { "tag_name": "v2.4.1", "name": "v2.4.1", "draft": false }
        ]
        """;

        var ok = UpdateService.TryGetLatestReleaseVersion(payload, out var latestVersion);

        Assert.True(ok);
        Assert.Equal(new Version(1, 2, 3), latestVersion);
    }

    [Fact]
    public void TryGetLatestReleaseVersion_FallsBackToReleaseName()
    {
        const string payload = """
        [
          { "tag_name": "release", "name": "v1.2.0", "draft": false }
        ]
        """;

        var ok = UpdateService.TryGetLatestReleaseVersion(payload, out var latestVersion);

        Assert.True(ok);
        Assert.Equal(new Version(1, 2, 0), latestVersion);
    }

    [Fact]
    public void TryGetLatestReleaseVersion_EmptyPayload_ReturnsFailure()
    {
        const string payload = "[]";

        var ok = UpdateService.TryGetLatestReleaseVersion(payload, out var latestVersion);

        Assert.False(ok);
        Assert.Null(latestVersion);
    }

    [Fact]
    public void TryGetLatestReleaseVersion_DraftRelease_ReturnsFailure()
    {
        const string payload = """
        [
          { "tag_name": "v1.5.0", "name": "v1.5.0", "draft": true }
        ]
        """;

        var ok = UpdateService.TryGetLatestReleaseVersion(payload, out var latestVersion);

        Assert.False(ok);
        Assert.Null(latestVersion);
    }

    [Fact]
    public void TryGetLatestReleaseVersion_UnparseableRelease_ReturnsFailure()
    {
        const string payload = """
        [
          { "tag_name": "not-a-version", "name": "still-not-a-version", "draft": false }
        ]
        """;

        var ok = UpdateService.TryGetLatestReleaseVersion(payload, out var latestVersion);

        Assert.False(ok);
        Assert.Null(latestVersion);
    }
}
