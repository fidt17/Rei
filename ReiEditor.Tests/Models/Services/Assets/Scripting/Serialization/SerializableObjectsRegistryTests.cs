using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting.Serialization;

/// <summary>Verifies serializable definition refresh and normalized object lookup.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class SerializableObjectsRegistryTests : IDisposable
{
    /// <summary>Provides isolated engine source directory.</summary>
    private sealed class TestEngineSettingsProvider(string enginePath) : IEngineSettingsProvider
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public string GetEnginePath() => enginePath;
        public string GetEngineDebugIncludeDir() => enginePath;
        public string GetEngineReleaseIncludeDir() => enginePath;
        public string GetEngineSourceIncludes() => enginePath;
        public string GetEngineResourcesDir() => enginePath;
        public string GetEngineBehavioursDir() => enginePath;
        public string GetEngineVersion() => "test";
    }

    private readonly TemporaryProjectFixture _project = new();
    private readonly SerializableObjectsRegistry _registry;
    private readonly SourceFilesUtility _sourceFiles;

    /// <summary>Creates registry over isolated project and engine roots.</summary>
    public SerializableObjectsRegistryTests()
    {
        var enginePath = _project.Directory.GetPath("Engine");
        Directory.CreateDirectory(_project.Resources.GetScriptsPath());
        Directory.CreateDirectory(enginePath);
        _sourceFiles = new SourceFilesUtility(
            _project.Resources,
            new TestEngineSettingsProvider(enginePath),
            new TestLogger<SourceFilesUtility>());
        _registry = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
    }

    /// <summary>Refresh replaces objects and lookup strips concrete template arguments.</summary>
    [Fact]
    public void RefreshesObjectsAndResolvesTemplateInstances()
    {
        var path = _project.Resources.GetScriptsPath("Container.h");
        File.WriteAllText(path, "template<typename T> class Container { SERIALIZABLE_BODY(Container) SERIALIZE int Size; };");

        RefreshRegistry();

        Assert.Equal("Container", Assert.Single(_registry.GetObjects()).ObjectName);
        Assert.NotNull(_registry.GetObject("Container<Texture>"));

        File.WriteAllText(path, "class Replacement { SERIALIZABLE_BODY(Replacement) SERIALIZE bool Active; };");
        RefreshRegistry();

        Assert.Null(_registry.GetObject("Container"));
        Assert.Equal("Replacement", Assert.Single(_registry.GetObjects()).ObjectName);
    }

    /// <summary>Second refresh removes enum definitions deleted from source and publishes replacement definitions.</summary>
    [Fact]
    public void RefreshRemovesDeletedEnums()
    {
        var path = _project.Resources.GetScriptsPath("State.h");
        File.WriteAllText(path, "SERIALIZABLE_ENUM(OldState) { Ready };");
        RefreshRegistry();
        Assert.NotNull(_registry.GetEnum("OldState"));

        File.WriteAllText(path, "SERIALIZABLE_ENUM(NewState) { Waiting };");
        RefreshRegistry();

        Assert.Null(_registry.GetEnum("OldState"));
        Assert.NotNull(_registry.GetEnum("NewState"));
    }

    /// <summary>Refresh removes all prior enum definitions when sources no longer contain enums.</summary>
    [Fact]
    public void RefreshRemovesAllEnums()
    {
        var path = _project.Resources.GetScriptsPath("State.h");
        File.WriteAllText(path, "SERIALIZABLE_ENUM(State) { Ready };");
        RefreshRegistry();
        Assert.NotNull(_registry.GetEnum("State"));

        File.WriteAllText(path, "class NoEnums {};");
        RefreshRegistry();

        Assert.Null(_registry.GetEnum("State"));
    }

    /// <summary>Refresh replaces values for enum definitions that retain the same name.</summary>
    [Fact]
    public void RefreshReplacesChangedEnumValues()
    {
        var path = _project.Resources.GetScriptsPath("State.h");
        File.WriteAllText(path, "SERIALIZABLE_ENUM(State) { Ready = 2, Waiting };");
        RefreshRegistry();
        Assert.Equal(new Dictionary<string, int> { ["Ready"] = 2, ["Waiting"] = 3 }, _registry.GetEnum("State")!.Options);

        File.WriteAllText(path, "SERIALIZABLE_ENUM(State) { Disabled = 7 };");
        RefreshRegistry();

        Assert.Equal(new Dictionary<string, int> { ["Disabled"] = 7 }, _registry.GetEnum("State")!.Options);
    }

    /// <summary>Repeated refreshes keep only latest same-name enum definition visible.</summary>
    [Fact]
    public void RepeatedRefreshesKeepLatestEnumDefinition()
    {
        var path = _project.Resources.GetScriptsPath("State.h");
        SerializableEnum? previous = null;

        for (var value = 0; value < 3; value++)
        {
            File.WriteAllText(path, $"SERIALIZABLE_ENUM(State) {{ Current = {value} }};");
            RefreshRegistry();
            var current = _registry.GetEnum("State");

            Assert.NotNull(current);
            Assert.Equal(value, current.Options["Current"]);
            Assert.NotSame(previous, current);
            previous = current;
        }
    }

    private void RefreshRegistry()
    {
        var definitions = _sourceFiles.ProcessFiles();
        _registry.Replace(definitions.SerializableObjects, definitions.SerializableEnums);
    }

    /// <summary>Deletes isolated registry roots.</summary>
    public void Dispose() => _project.Dispose();
}
