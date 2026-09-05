using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting;

/// <summary>Verifies supported C++ reflection macro parsing across project and engine headers.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class SourceFilesUtilityTests : IDisposable
{
    /// <summary>Provides isolated engine paths required by source discovery.</summary>
    private sealed class TestEngineSettingsProvider(string enginePath) : IEngineSettingsProvider
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public string GetEnginePath() => enginePath;
        public string GetEngineDebugIncludeDir() => enginePath;
        public string GetEngineReleaseIncludeDir() => enginePath;
        public string GetEngineSourceIncludes() => enginePath;
        public string GetEngineResourcesDir() => Path.Combine(enginePath, "Resources");
        public string GetEngineBehavioursDir() => Path.Combine(enginePath, "Behaviours");
        public string GetEngineVersion() => "test";
    }

    private readonly TemporaryProjectFixture _project = new();
    private readonly string _enginePath;
    private readonly TestLogger<SourceFilesUtility> _logger = new();
    private readonly SourceFilesUtility _utility;

    /// <summary>Creates isolated project and engine source roots.</summary>
    public SourceFilesUtilityTests()
    {
        _enginePath = _project.Directory.GetPath("Engine");
        Directory.CreateDirectory(_project.Resources.GetScriptsPath());
        Directory.CreateDirectory(_enginePath);
        _utility = new SourceFilesUtility(_project.Resources, new TestEngineSettingsProvider(_enginePath), _logger);
    }

    /// <summary>Parses namespace, template marker, supported property kinds, defaults, and editor visibility.</summary>
    [Fact]
    public void ParsesSerializableObjectProperties()
    {
        const string SOURCE = """
            namespace game {
            template<typename T>
            class Inventory {
                SERIALIZABLE_BODY(Inventory)
                SERIALIZE int Count = 7;
                HIDE_IN_EDITOR SERIALIZE rei::vector<AssetRef<Texture>> Icons;
                SERIALIZE game::Settings Config;
            };
            }
            """;

        Assert.True(_utility.TryGetSerializableObjectNameFrom(SOURCE, out var name, out var isTemplate));
        var properties = _utility.GetSerializedProperties(SOURCE);

        Assert.Equal("Inventory", name);
        Assert.True(isTemplate);
        Assert.Equal("game", SourceFilesUtility.GetObjectNamespaceFrom(SOURCE, "Inventory.h"));
        Assert.Equal(SerializedTypeEnum.Integer, properties["Count"].Type);
        Assert.Equal("7", properties["Count"].DefaultValue);
        Assert.Equal(SerializedTypeEnum.Collection, properties["Icons"].Type);
        Assert.Equal("AssetRef<Texture>", properties["Icons"].ItemSourceType);
        Assert.Equal("Texture", properties["Icons"].ItemTemplateTypeName);
        Assert.True(properties["Icons"].HideInEditor);
        Assert.Equal(SerializedTypeEnum.Custom, properties["Config"].Type);
    }

    /// <summary>Separate block comments hide only their own contents and preserve code between them.</summary>
    [Fact]
    public void RemovesSeparateBlockCommentsWithoutConsumingInterveningProperties()
    {
        const string SOURCE = """
            /* SERIALIZE int HiddenOne; */
            SERIALIZE int VisibleOne;
            /* SERIALIZE int HiddenTwo; */
            SERIALIZE float VisibleTwo;
            """;

        var properties = _utility.GetSerializedProperties(SOURCE);

        Assert.Equal(new[] { "VisibleOne", "VisibleTwo" }, properties.Keys);
    }

    /// <summary>Required component names are trimmed, namespace-normalized, deduplicated, and placeholder-free.</summary>
    [Fact]
    public void ParsesNormalizedUniqueRequiredComponents()
    {
        const string SOURCE = """
            REQUIRE_COMPONENT( game::Transform )
            REQUIRE_COMPONENT(Transform)
            REQUIRE_COMPONENT(COMPONENT_NAME)
            // REQUIRE_COMPONENT(Hidden)
            """;

        Assert.Equal(new[] { "Transform" }, _utility.GetRequiredComponentNames(SOURCE));
    }

    /// <summary>Enum parser applies explicit positive values and continues implicit numbering.</summary>
    [Fact]
    public void ProcessesSerializableEnumValues()
    {
        WriteHeader(_project.Resources.GetScriptsPath("State.h"), "SERIALIZABLE_ENUM(State) { Idle, Running = 4, Paused };");

        var result = _utility.ProcessFiles();
        var parsedEnum = Assert.Single(result.SerializableEnums);

        Assert.True(_utility.AreSourceFilesValid);
        Assert.Equal(new Dictionary<string, int> { ["Idle"] = 0, ["Running"] = 4, ["Paused"] = 5 }, parsedEnum.Options);
    }

    /// <summary>One malformed header marks source invalid while later valid headers still contribute definitions.</summary>
    [Fact]
    public void ContinuesProcessingAfterHeaderParseFailure()
    {
        WriteHeader(_project.Resources.GetScriptsPath("Broken.h"), "SERIALIZABLE_BODY(Broken)\nSERIALIZE int MissingSemicolon");
        WriteHeader(Path.Combine(_enginePath, "Good.h"), "SERIALIZABLE_BODY(Good)\nSERIALIZE bool Enabled;");

        var result = _utility.ProcessFiles();

        Assert.False(_utility.AreSourceFilesValid);
        Assert.Contains(result.SerializableObjects, item => item.ObjectName == "Good");
        Assert.Contains(_logger.Entries, entry => entry.Message.Contains("Broken.h"));
    }

    /// <summary>Multiple serializable enums in one header reject that header without stopping other files.</summary>
    [Fact]
    public void RejectsMultipleEnumsInOneHeader()
    {
        WriteHeader(_project.Resources.GetScriptsPath("Enums.h"), "SERIALIZABLE_ENUM(First) { A }; SERIALIZABLE_ENUM(Second) { B };");

        var result = _utility.ProcessFiles();

        Assert.False(_utility.AreSourceFilesValid);
        Assert.Empty(result.SerializableEnums);
    }

    /// <summary>Writes source text under an owned temporary root.</summary>
    private static void WriteHeader(string path, string source)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, source);
    }

    /// <summary>Deletes isolated source roots.</summary>
    public void Dispose() => _project.Dispose();
}
