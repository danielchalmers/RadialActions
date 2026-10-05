using System.Globalization;
using System.Runtime.InteropServices;

namespace RadialActions.Tests;

public sealed class AboutSettingsViewModelTests
{
    [Theory]
    [InlineData("1.4.0.0", "1.4.0")]
    [InlineData("1.4.2", "1.4.2")]
    [InlineData("2.0", "2.0.0")]
    [InlineData("10.11.12.13", "10.11.12")]
    public void FormatVersion_ShowsThreeParts(string version, string expected)
    {
        Assert.Equal(expected, AppInfoFormatter.FormatVersion(version));
        Assert.Equal(expected, AppInfoFormatter.FormatVersion(Version.Parse(version)));
    }

    [Fact]
    public void FormatVersion_NotAVersion_ReturnsInputUnchanged()
    {
        Assert.Equal("dev build", AppInfoFormatter.FormatVersion("dev build"));
        Assert.Null(AppInfoFormatter.FormatVersion((string)null));
        Assert.Equal(string.Empty, AppInfoFormatter.FormatVersion((Version)null));
    }

    [Theory]
    [InlineData(Architecture.X64, "x64")]
    [InlineData(Architecture.X86, "x86")]
    [InlineData(Architecture.Arm64, "ARM64")]
    [InlineData(Architecture.Arm, "ARM")]
    public void FormatArchitecture_UsesWindowsSpelling(Architecture architecture, string expected)
    {
        Assert.Equal(expected, AppInfoFormatter.FormatArchitecture(architecture));
    }

    [Fact]
    public void VersionTextConverter_FormatsVersionsAndIgnoresEverythingElse()
    {
        var converter = new VersionTextConverter();

        Assert.Equal("1.4.0", converter.Convert(new Version(1, 4), typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void GetSettingsFileMissingMessage_NamesThePath()
    {
        Assert.Equal(
            @"Couldn't create the settings file at C:\Apps\RadialActions.settings.",
            AboutSettingsViewModel.GetSettingsFileMissingMessage(@"C:\Apps\RadialActions.settings"));
    }

    [Fact]
    public void GetOpenInNotepadFailedMessage_ExplainsTheFailureAndWhereTheFileIs()
    {
        var message = AboutSettingsViewModel.GetOpenInNotepadFailedMessage("the settings file", "The system cannot find the file specified.", @"C:\Apps\RadialActions.settings");

        Assert.Equal(
            "Couldn't open the settings file in Notepad.\n\nThe system cannot find the file specified.\n\nThe file is at C:\\Apps\\RadialActions.settings.",
            message);
    }

    [Fact]
    public void Properties_UseDisplayFormats()
    {
        var viewModel = new AboutSettingsViewModel(RadialActions.Properties.Settings.DeserializeFromJson("{}"));

        Assert.Equal(AppInfoFormatter.FormatArchitecture(RuntimeInformation.ProcessArchitecture), viewModel.Architecture);
        Assert.True(viewModel.AppVersion.Split('.').Length <= 3, $"Expected at most three version parts but got \"{viewModel.AppVersion}\".");
    }
}
