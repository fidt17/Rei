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

    /// <summary>Creates registry over isolated project and engine roots.</summary>
    public SerializableObjectsRegistryTests()
    {
        var enginePath = _project.Directory.GetPath("Engine");
        Directory.CreateDirectory(_project.Resources.GetScriptsPath());
        Directory.CreateDirectory(enginePath);
        var sourceFiles = new SourceFilesUtility(
            _project.Resources,
            new TestEngineSettingsProvider(enginePath),
            new TestLogger<SourceFilesUtility>());
        _registry = new SerializableObjectsRegistry(sourceFiles, new TestLogger<SerializableObjectsRegistry>());
    }

    /// <summary>Refresh replaces objects and lookup strips concrete template arguments.</summary>
    [Fact]
    public async Task RefreshesObjectsAndResolvesTemplateInstances()
    {
        var path = _project.Resources.GetScriptsPath("Container.h");
        File.WriteAllText(path, "template<typename T> class Container { SERIALIZABLE_BODY(Container) SERIALIZE int Size; };");

        await _registry.Refresh();

        Assert.Equal("Container", Assert.Single(_registry.GetObjects()).ObjectName);
        Assert.NotNull(_registry.GetObject("Container<Texture>"));

        File.WriteAllText(path, "class Replacement { SERIALIZABLE_BODY(Replacement) SERIALIZE bool Active; };");
        await _registry.Refresh();

        Assert.Null(_registry.GetObject("Container"));
        Assert.Equal("Replacement", Assert.Single(_registry.GetObjects()).ObjectName);
    }

    /// <summary>Second refresh removes enum definitions deleted from source and publishes replacement definitions.</summary>
    [Fact]
    public async Task RefreshRemovesDeletedEnums()
    {
        var path = _project.Resources.GetScriptsPath("State.h");
        File.WriteAllText(path, "SERIALIZABLE_ENUM(OldState) { Ready };");
        await _registry.Refresh();
        Assert.NotNull(_registry.GetEnum("OldState"));

        File.WriteAllText(path, "SERIALIZABLE_ENUM(NewState) { Waiting };");
        await _registry.Refresh();

        Assert.Null(_registry.GetEnum("OldState"));
        Assert.NotNull(_registry.GetEnum("NewState"));
    }

    /// <summary>Deletes isolated registry roots.</summary>
    public void Dispose() => _project.Dispose();
}
