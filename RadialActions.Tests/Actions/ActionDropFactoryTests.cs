using System.IO;
using System.Text;
using System.Windows;

namespace RadialActions.Tests;

public sealed class ActionDropFactoryTests : IDisposable
{
    private const string UniformResourceLocatorW = "UniformResourceLocatorW";
    private const string UniformResourceLocator = "UniformResourceLocator";
    private const string MozillaUrl = "text/x-moz-url";

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "RadialActions.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ExtractTargets_NullData_ReturnsEmpty()
    {
        Assert.Empty(ActionDropFactory.ExtractTargets(null));
    }

    [Fact]
    public void ExtractTargets_EmptyData_ReturnsEmpty()
    {
        Assert.Empty(ActionDropFactory.ExtractTargets(new FakeDataObject()));
    }

    [Fact]
    public void ExtractTargets_FileDrop_ReturnsPathsInOrder()
    {
        var data = new FakeDataObject().Set(DataFormats.FileDrop, new[] { @"C:\Apps\First.exe", @"C:\Docs\Second.txt" });

        Assert.Equal([@"C:\Apps\First.exe", @"C:\Docs\Second.txt"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_FileDrop_DeduplicatesIgnoringCase()
    {
        var data = new FakeDataObject().Set(DataFormats.FileDrop, new[] { @"C:\Apps\First.exe", @"c:\apps\first.exe" });

        Assert.Equal([@"C:\Apps\First.exe"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_PrefersFileDropOverText()
    {
        var data = new FakeDataObject()
            .Set(DataFormats.FileDrop, new[] { @"C:\Apps\First.exe" })
            .Set(DataFormats.UnicodeText, "https://example.com");

        Assert.Equal([@"C:\Apps\First.exe"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_UnicodeTextUrl_ReturnsUrl()
    {
        var data = new FakeDataObject().Set(DataFormats.UnicodeText, "https://example.com/path");

        Assert.Equal(["https://example.com/path"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_UniformResourceLocatorStream_DecodesNullTerminatedUrl()
    {
        var bytes = Encoding.Unicode.GetBytes("https://example.com\0");
        var data = new FakeDataObject().Set(UniformResourceLocatorW, new MemoryStream(bytes));

        Assert.Equal(["https://example.com"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_MozillaUrl_KeepsUrlLineOnly()
    {
        var bytes = Encoding.Unicode.GetBytes("https://example.com/\r\nExample Title");
        var data = new FakeDataObject().Set(MozillaUrl, new MemoryStream(bytes));

        Assert.Equal(["https://example.com/"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_PlainText_IsIgnored()
    {
        var data = new FakeDataObject().Set(DataFormats.UnicodeText, "just some dragged words");

        Assert.Empty(ActionDropFactory.ExtractTargets(data));
    }

    [Theory]
    [InlineData("Re: Meeting notes")]
    [InlineData("TODO: fix the drag handler")]
    [InlineData("Note: call John tomorrow")]
    [InlineData("std::string")]
    public void ExtractTargets_SchemeLikeProse_IsIgnored(string text)
    {
        var data = new FakeDataObject().Set(DataFormats.UnicodeText, text);

        Assert.Empty(ActionDropFactory.ExtractTargets(data));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path")]
    [InlineData("ftp://host/file.zip")]
    [InlineData("mailto:user@example.com")]
    public void ExtractTargets_LaunchableSchemes_AreAccepted(string url)
    {
        var data = new FakeDataObject().Set(DataFormats.UnicodeText, url);

        Assert.Equal([url], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_TextFallback_ReturnsUrl()
    {
        var data = new FakeDataObject().Set(DataFormats.Text, "https://example.com/path");

        Assert.Equal(["https://example.com/path"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_AnsiUniformResourceLocatorStream_ReturnsUrl()
    {
        var bytes = Encoding.Latin1.GetBytes("https://example.com\0");
        var data = new FakeDataObject().Set(UniformResourceLocator, new MemoryStream(bytes));

        Assert.Equal(["https://example.com"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_PrefersDedicatedUrlFormatOverText()
    {
        var canonical = Encoding.Unicode.GetBytes("https://canonical.com\0");
        var data = new FakeDataObject()
            .Set(UniformResourceLocatorW, new MemoryStream(canonical))
            .Set(DataFormats.UnicodeText, "https://fallback.com");

        Assert.Equal(["https://canonical.com"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_WhitespaceOnlyText_ReturnsEmpty()
    {
        var data = new FakeDataObject().Set(DataFormats.UnicodeText, "   \r\n");

        Assert.Empty(ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void ExtractTargets_FileDrop_SkipsEmptyEntries()
    {
        var data = new FakeDataObject().Set(DataFormats.FileDrop, new[] { "", "   ", @"C:\Apps\First.exe" });

        Assert.Equal([@"C:\Apps\First.exe"], ActionDropFactory.ExtractTargets(data));
    }

    [Fact]
    public void CreateAction_BlankTarget_UsesDefaultNameAndFileIcon()
    {
        var action = ActionDropFactory.CreateAction(string.Empty);

        Assert.Equal(ActionType.Open, action.Type);
        Assert.Equal(PieAction.DefaultName, action.Name);
        Assert.Equal(OpenActionDefaults.FileIcon, action.Icon);
    }

    [Fact]
    public void CreateAction_Url_FillsWebDefaults()
    {
        var action = ActionDropFactory.CreateAction("https://example.com/path");

        Assert.Equal(ActionType.Open, action.Type);
        Assert.Equal("https://example.com/path", action.Parameter);
        Assert.Equal("example.com", action.Name);
        Assert.Equal(OpenActionDefaults.WebIcon, action.Icon);
        Assert.Equal(string.Empty, action.WorkingDirectory);
    }

    [Fact]
    public void CreateAction_Directory_FillsFolderDefaults()
    {
        Directory.CreateDirectory(_tempRoot);
        var folder = Path.Combine(_tempRoot, "Games");
        Directory.CreateDirectory(folder);

        var action = ActionDropFactory.CreateAction(folder);

        Assert.Equal(ActionType.Open, action.Type);
        Assert.Equal(folder, action.Parameter);
        Assert.Equal("Games", action.Name);
        Assert.Equal(OpenActionDefaults.FolderIcon, action.Icon);
        Assert.Equal(folder, action.WorkingDirectory);
    }

    [Fact]
    public void CreateAction_File_FillsFileDefaults()
    {
        Directory.CreateDirectory(_tempRoot);
        var file = Path.Combine(_tempRoot, "Notes.txt");
        File.WriteAllText(file, string.Empty);

        var action = ActionDropFactory.CreateAction(file);

        Assert.Equal(ActionType.Open, action.Type);
        Assert.Equal(file, action.Parameter);
        Assert.Equal("Notes", action.Name);
        Assert.Equal(OpenActionDefaults.FileIcon, action.Icon);
        Assert.Equal(_tempRoot, action.WorkingDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private sealed class FakeDataObject : IDataObject
    {
        private readonly Dictionary<string, object> _data = new(StringComparer.Ordinal);

        public FakeDataObject Set(string format, object value)
        {
            _data[format] = value;
            return this;
        }

        public object GetData(string format) => _data.TryGetValue(format, out var value) ? value : null;
        public object GetData(string format, bool autoConvert) => GetData(format);
        public object GetData(Type format) => GetData(format.FullName);

        public bool GetDataPresent(string format) => _data.ContainsKey(format);
        public bool GetDataPresent(string format, bool autoConvert) => GetDataPresent(format);
        public bool GetDataPresent(Type format) => GetDataPresent(format.FullName);

        public string[] GetFormats() => [.. _data.Keys];
        public string[] GetFormats(bool autoConvert) => GetFormats();

        public void SetData(object data) => throw new NotSupportedException();
        public void SetData(string format, object data) => Set(format, data);
        public void SetData(Type format, object data) => throw new NotSupportedException();
        public void SetData(string format, object data, bool autoConvert) => Set(format, data);
    }
}
