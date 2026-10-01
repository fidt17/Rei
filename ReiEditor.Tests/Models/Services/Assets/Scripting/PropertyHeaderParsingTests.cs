using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting;

[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class PropertyHeaderParsingTests : IDisposable
{
    private sealed class EngineSettings(string path) : IEngineSettingsProvider
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public string GetEnginePath() => path;
        public string GetEngineDebugIncludeDir() => path;
        public string GetEngineReleaseIncludeDir() => path;
        public string GetEngineSourceIncludes() => path;
        public string GetEngineResourcesDir() => path;
        public string GetEngineBehavioursDir() => path;
        public string GetEngineVersion() => "test";
    }

    private readonly TemporaryProjectFixture _project = new();
    private readonly TestLogger<SourceFilesUtility> _logger = new();
    private readonly SourceFilesUtility _utility;

    public PropertyHeaderParsingTests()
    {
        var enginePath = _project.Directory.GetPath("Engine");
        Directory.CreateDirectory(enginePath);
        Directory.CreateDirectory(_project.Resources.GetScriptsPath());
        _utility = new SourceFilesUtility(_project.Resources, new EngineSettings(enginePath), _logger);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void HeadersFollowDeclarationsAcrossCommentsAndLines(string newline)
    {
        var source = "REI_HEADER(\"Palette \\\"warm\\\"\") /* ignored */" + newline +
                     "SERIALIZE float Zulu = 1;" + newline +
                     "SERIALIZE int Alpha = 2;" + newline +
                     "REI_HEADER(\"Diagnostics\") HIDE_IN_EDITOR SERIALIZE bool Hidden = true;";
        var properties = _utility.GetSerializedProperties(source);
        Assert.Equal(new[] { "Zulu", "Alpha", "Hidden" }, properties.Keys);
        Assert.Equal("Palette \"warm\"", properties["Zulu"].HeaderBefore);
        Assert.Null(properties["Alpha"].HeaderBefore);
        Assert.True(properties["Hidden"].HideInEditor);
        Assert.Equal(new[] { 0, 1, 2 }, properties.Values.Select(property => property.DeclarationIndex));
    }

    [Fact]
    public void MacroNamesAndSemicolonsInsideLiteralsAreNotDeclarations()
    {
        const string SOURCE = """
            const char* ignored = R"tag(SERIALIZE int Ghost; REI_HEADER("Ghost"))tag";
            // REI_HEADER("Comment") SERIALIZE int Comment;
            REI_HEADER("SERIALIZE; details") SERIALIZE float Speed = 3;
            SERIALIZE std::string Message = "SERIALIZE;";
            SERIALIZE bool Enabled = true;
            """;
        var properties = _utility.GetSerializedProperties(SOURCE);
        Assert.Equal(new[] { "Speed", "Message", "Enabled" }, properties.Keys);
        Assert.Equal("SERIALIZE; details", properties["Speed"].HeaderBefore);
        Assert.Null(properties["Message"].HeaderBefore);
    }

    [Theory]
    [InlineData("REI_HEADER() SERIALIZE int Value;")]
    [InlineData("REI_HEADER(\"\") SERIALIZE int Value;")]
    [InlineData("REI_HEADER(Title) SERIALIZE int Value;")]
    [InlineData("REI_HEADER(\"A\", \"B\") SERIALIZE int Value;")]
    [InlineData("REI_HEADER(\"A\") REI_HEADER(\"B\") SERIALIZE int Value;")]
    [InlineData("REI_HEADER(\"A\") int Plain; SERIALIZE int Value;")]
    [InlineData("REI_HEADER(\"A\")")]
    public void RejectsMalformedOrDetachedHeaders(string source)
    {
        Assert.Throws<FormatException>(() => _utility.GetSerializedProperties(source));
    }

    [Fact]
    public void ParseFailureReportsFileAndKeepsOtherDefinitions()
    {
        File.WriteAllText(_project.Resources.GetScriptsPath("Broken.h"), "DATA_ASSET_BODY(Broken)\nREI_HEADER() SERIALIZE int Value;");
        File.WriteAllText(_project.Resources.GetScriptsPath("Good.h"), "DATA_ASSET_BODY(Good)\nREI_HEADER(\"Settings\") SERIALIZE int Value;");
        var result = _utility.ProcessFiles();
        Assert.False(_utility.AreSourceFilesValid);
        Assert.Contains(result.DataAssetDeclarations, item => item.ObjectName == "Good");
        Assert.Contains(_logger.Entries, entry => entry.Message.Contains("Broken.h"));
    }

    [Fact]
    public async Task AddingHeadersDoesNotChangeGeneratedNativeSerialization()
    {
        var plain = new SerializableObjectInfo("game", "Config", false, new ObjectFile<string>("", "Config.h"), _utility.GetSerializedProperties("SERIALIZE int Count = 4; SERIALIZE float Speed = 2;"), "Config.h");
        var decorated = new SerializableObjectInfo("game", "Config", false, new ObjectFile<string>("", "Config.h"), _utility.GetSerializedProperties("REI_HEADER(\"Grid\") SERIALIZE int Count = 4; REI_HEADER(\"Animation\") SERIALIZE float Speed = 2;"), "Config.h");
        var registry = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
        var resources = new TestResourceService(_project.Resources);
        var generator = new BehaviourRegistrySourceGenerator(resources, registry);
        await generator.GenerateBehaviourRegistrySourceFile(new(), new[] { plain }, []);
        await generator.GenerateBehaviourRegistrySourceFile(new(), new[] { decorated }, []);
        Assert.Equal(resources.Writes[0].Data, resources.Writes[1].Data);
    }

    public void Dispose() => _project.Dispose();
}
