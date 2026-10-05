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

    [Fact]
    public void IsUpdateAvailable_InstalledFileVersionMatchesReleaseTag_ReturnsFalse()
    {
        // The exe reports a four-part file version while release tags have three parts.
        var isAvailable = UpdateService.IsUpdateAvailable(new Version(0, 7, 0, 0), UpdateService.TryParseVersion("v0.7.0"));

        Assert.False(isAvailable);
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V2.0", "2.0")]
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
    [InlineData("v1.2.3-preview1")]
    [InlineData("release-10.4.7-beta1")]
    [InlineData("no-version-here")]
    public void TryParseVersion_InvalidFormats_ReturnsNull(string raw)
    {
        var parsed = UpdateService.TryParseVersion(raw);

        Assert.Null(parsed);
    }

    [Fact]
    public void TryGetLatestReleaseVersion_StableReleaseWithInstaller_ReturnsItsVersion()
    {
        const string payload = """
        {
          "tag_name": "v3.0.0",
          "draft": false,
          "prerelease": false,
          "assets": [
            { "name": "RadialActions-3.0.0-x64.zip" },
            { "name": "RadialActions-3.0.0-x64.msi" }
          ]
        }
        """;

        var ok = UpdateService.TryGetLatestReleaseVersion(payload, out var latestVersion);

        Assert.True(ok);
        Assert.Equal(new Version(3, 0, 0), latestVersion);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""[{ "name": "RadialActions-3.0.0-x64.zip" }, { "name": "RadialActions-3.0.0-arm64.zip" }]""")]
    [InlineData("null")]
    public void TryGetLatestReleaseVersion_ReleaseWithoutInstaller_ReturnsFailure(string assets)
    {
        var payload = $$"""{ "tag_name": "v3.0.0", "draft": false, "prerelease": false, "assets": {{assets}} }""";

        var ok = UpdateService.TryGetLatestReleaseVersion(payload, out var latestVersion);

        Assert.False(ok);
        Assert.Null(latestVersion);
    }

    [Theory]
    [InlineData("true", "false")]
    [InlineData("false", "true")]
    public void TryGetLatestReleaseVersion_DraftOrPrerelease_ReturnsFailure(string draft, string prerelease)
    {
        var payload = $$"""
        {
          "tag_name": "v3.0.0",
          "draft": {{draft}},
          "prerelease": {{prerelease}},
          "assets": [{ "name": "RadialActions-3.0.0-x64.msi" }]
        }
        """;

        var ok = UpdateService.TryGetLatestReleaseVersion(payload, out var latestVersion);

        Assert.False(ok);
        Assert.Null(latestVersion);
    }

    [Fact]
    public void TryGetLatestReleaseVersion_UnparseableTag_ReturnsFailure()
    {
        const string payload = """
        { "tag_name": "not-a-version", "draft": false, "prerelease": false, "assets": [{ "name": "RadialActions.msi" }] }
        """;

        var ok = UpdateService.TryGetLatestReleaseVersion(payload, out var latestVersion);

        Assert.False(ok);
        Assert.Null(latestVersion);
    }

    [Fact]
    public void TryGetLatestReleaseVersion_NullPayload_ReturnsFailure()
    {
        var ok = UpdateService.TryGetLatestReleaseVersion("null", out var latestVersion);

        Assert.False(ok);
        Assert.Null(latestVersion);
    }
}
