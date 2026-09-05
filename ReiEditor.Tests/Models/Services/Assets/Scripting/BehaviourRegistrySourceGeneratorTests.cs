using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting;

/// <summary>Verifies generated behaviour registration, serialization, and dependency source.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class BehaviourRegistrySourceGeneratorTests : IDisposable
{
    /// <summary>Returns configured serializable object and enum definitions.</summary>
    private sealed class TestSerializableObjectsRegistry : ISerializableObjectsRegistry
    {
        public List<SerializableObjectInfo> Objects { get; } = new();
        public List<SerializableEnum> Enums { get; } = new();

        /// <summary>Returns configured object definitions.</summary>
        public IEnumerable<SerializableObjectInfo> GetObjects() => Objects;

        /// <summary>Completes without source discovery.</summary>
        public Task Refresh() => Task.CompletedTask;

        /// <summary>Finds configured object definition by base name.</summary>
        public SerializableObjectInfo? GetObject(string objectName) => Objects.Find(item => item.ObjectName == objectName.Split('<')[0]);

        /// <summary>Finds configured enum definition by name.</summary>
        public SerializableEnum? GetEnum(string enumName) => Enums.Find(item => item.EnumName == enumName);
    }

    private readonly TemporaryProjectFixture _project = new();

    /// <summary>Generates source with includes, IDs, requirements, templates, and typed asset dependency handling.</summary>
    [Fact]
    public async Task GeneratesRegistrySerializationAndDependencyCode()
    {
        var registry = new TestSerializableObjectsRegistry();
        var assetRef = Property(SerializedTypeEnum.Custom, "AssetRef<Texture>", "Texture");
        var settings = new SerializableObjectInfo(
            "game",
            "Settings",
            false,
            new ObjectFile<string>("", "Settings.h"),
            new Dictionary<string, SerializableObjectInfo.SerializedPropertyData> { ["Icon"] = assetRef },
            "Settings.h");
        registry.Objects.Add(settings);

        var required = Behaviour("game", "Transform", 2, "Transform.h", new(), Array.Empty<string>());
        var player = Behaviour(
            "game",
            "Player",
            7,
            "Player.h",
            new Dictionary<string, SerializableObjectInfo.SerializedPropertyData>
            {
                ["Settings"] = Property(SerializedTypeEnum.Custom, "game::Settings"),
                ["Portrait"] = assetRef,
                ["Frames"] = Property(
                    SerializedTypeEnum.Collection,
                    "vector<AssetRef<Texture>>",
                    "AssetRef<Texture>",
                    SerializedTypeEnum.Custom,
                    "AssetRef<Texture>",
                    "Texture")
            },
            new[] { "Transform", "Missing" });
        var behaviours = new Dictionary<int, BehaviourAssetInfo> { [2] = required, [7] = player };
        var resources = new TestResourceService(_project.Resources);
        var generator = new BehaviourRegistrySourceGenerator(resources, registry);

        await generator.GenerateBehaviourRegistrySourceFile(behaviours, registry.GetObjects());

        var write = Assert.Single(resources.Writes);
        Assert.Equal(_project.Resources.GetProjectPath("Scripts", "Internal", "BehaviourRegistry.cpp"), write.Path);
        Assert.Contains("#include \"Settings.h\"", write.Data);
        Assert.Contains("#include \"Player.h\"", write.Data);
        Assert.Contains("RegisterAutoAssignHandler<rei::render::Texture>()", write.Data);
        Assert.Contains("f.RegisterComponent<game::Player>(7", write.Data);
        Assert.Contains("{2});", write.Data);
        Assert.Contains("void game::Settings::REI_SET", write.Data);
        Assert.Contains("CreateTypedAssetDependency<rei::render::Texture>", write.Data);
        Assert.Contains("FramesJson.push_back(itemValue.REI_GET())", write.Data);
        Assert.Contains("Frames.clear();", write.Data);
        Assert.Contains("typename decltype(Frames)::value_type itemValue;", write.Data);
        Assert.Contains("Frames.emplace_back(std::move(itemValue));", write.Data);
        Assert.Contains("for (auto& itemValue : Frames)", write.Data);
        Assert.Contains("""if (data.contains("Frames")) { const auto& rawValue = data.at("Frames"); const auto& assetRefValue = rawValue.contains("Value") ? rawValue.at("Value") : rawValue; for (const auto& assetRefItem : assetRefValue)""", write.Data);
        Assert.Contains("""if (data.contains("Portrait")) { const auto& rawValue = data.at("Portrait"); const auto& assetRefValue = rawValue.contains("Value") ? rawValue.at("Value") : rawValue; if (assetRefValue.contains("Id"))""", write.Data);
        Assert.DoesNotContain("Missing", write.Data);
    }

    /// <summary>Creates behaviour definition for generated source scenarios.</summary>
    private static BehaviourAssetInfo Behaviour(
        string ns,
        string name,
        int id,
        string path,
        Dictionary<string, SerializableObjectInfo.SerializedPropertyData> properties,
        IReadOnlyList<string> required)
    {
        return new BehaviourAssetInfo(ns, name, id, new ObjectFile<string>("", path), properties, required, path);
    }

    /// <summary>Creates property metadata with explicit nested collection details.</summary>
    private static SerializableObjectInfo.SerializedPropertyData Property(
        SerializedTypeEnum type,
        string sourceType,
        string? templateType = null,
        SerializedTypeEnum itemType = SerializedTypeEnum.Invalid,
        string? itemSourceType = null,
        string? itemTemplateType = null)
    {
        return new SerializableObjectInfo.SerializedPropertyData(
            type,
            sourceType,
            templateType,
            itemType,
            itemSourceType,
            itemTemplateType,
            null,
            false);
    }

    /// <summary>Deletes isolated generated source output.</summary>
    public void Dispose() => _project.Dispose();
}
