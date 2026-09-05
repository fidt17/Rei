using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting;

/// <summary>Verifies behaviour header discovery and metadata path routing.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class BehaviourFileUtilityTests : IDisposable
{
    /// <summary>Provides isolated engine resource and behaviour directories.</summary>
    private sealed class TestEngineSettingsProvider(string enginePath) : IEngineSettingsProvider
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public string GetEnginePath() => enginePath;
        public string GetEngineDebugIncludeDir() => enginePath;
        public string GetEngineReleaseIncludeDir() => enginePath;
        public string GetEngineSourceIncludes() => enginePath;
        public string GetEngineResourcesDir() => Path.Combine(enginePath, "Resources");
        public string GetEngineBehavioursDir() => Path.Combine(GetEngineResourcesDir(), "Behaviours");
        public string GetEngineVersion() => "test";
    }

    private readonly TemporaryProjectFixture _project = new();
    private readonly TemporaryDirectory _engineDirectory = new();
    private readonly TestEngineSettingsProvider _engineSettings;
    private readonly BehaviourFileUtility _utility;

    /// <summary>Creates isolated project, internal, and engine behaviour roots.</summary>
    public BehaviourFileUtilityTests()
    {
        var enginePath = _engineDirectory.RootPath;
        _engineSettings = new TestEngineSettingsProvider(enginePath);
        Directory.CreateDirectory(_project.Resources.GetScriptsPath());
        Directory.CreateDirectory(_project.Resources.GetRootPath("Internal", "Behaviours"));
        Directory.CreateDirectory(_engineSettings.GetEngineBehavioursDir());
        _utility = new BehaviourFileUtility(_project.Resources, _engineSettings);
    }

    /// <summary>Discovers project and engine behaviours while redirecting engine metadata into project Internal resources.</summary>
    [Fact]
    public void DiscoversBehavioursAndRedirectsEngineMetaPath()
    {
        var projectHeader = WriteHeader(_project.Resources.GetScriptsPath("Player.h"), "BEHAVIOUR_BODY(Player)");
        var engineHeader = WriteHeader(Path.Combine(_engineSettings.GetEngineBehavioursDir(), "Physics", "RigidBody.h"), "BEHAVIOUR_BODY(RigidBody)");
        WriteHeader(_project.Resources.GetScriptsPath("Plain.h"), "class Plain {};");

        var behaviours = _utility.GetAllBehaviours();

        Assert.Equal(2, behaviours.Count);
        var project = Assert.Single(behaviours, item => item.Path == projectHeader);
        Assert.False(project.IsEngineBehaviour);
        Assert.Equal(projectHeader + ".meta", project.MetaPath);
        var engine = Assert.Single(behaviours, item => item.Path == engineHeader);
        Assert.True(engine.IsEngineBehaviour);
        Assert.Equal(_project.Resources.GetRootPath("Internal", "Behaviours", "Physics", "RigidBody.h.meta"), engine.MetaPath);
    }

    /// <summary>Behaviour name extraction rejects ordinary headers and returns macro argument.</summary>
    [Theory]
    [InlineData("BEHAVIOUR_BODY(Player)", true, "Player")]
    [InlineData("class Player {};", false, "")]
    public void ExtractsBehaviourName(string source, bool expectedResult, string expectedName)
    {
        Assert.Equal(expectedResult, _utility.TryGetBehaviourNameFrom(source, out var name));
        Assert.Equal(expectedName, name);
    }

    /// <summary>File detection requires an existing header containing a behaviour macro.</summary>
    [Fact]
    public async Task DetectsOnlyExistingBehaviourHeaders()
    {
        var header = WriteHeader(_project.Resources.GetScriptsPath("Mover.h"), "BEHAVIOUR_BODY(Mover)");
        var source = WriteHeader(_project.Resources.GetScriptsPath("Mover.cpp"), "BEHAVIOUR_BODY(Mover)");

        Assert.True(await _utility.IsBehaviourFile(header));
        Assert.False(await _utility.IsBehaviourFile(source));
        Assert.False(await _utility.IsBehaviourFile(_project.Resources.GetScriptsPath("Missing.h")));
    }

    /// <summary>Metadata discovery returns only readable asset metadata carrying behaviour data.</summary>
    [Fact]
    public async Task LoadsOnlyBehaviourMetadata()
    {
        var behaviourMetaPath = _project.Resources.GetScriptsPath("Mover.h.meta");
        var ordinaryMetaPath = _project.Resources.GetScriptsPath("Texture.png.meta");
        await _project.Resources.Write("{\"AssetId\":\"behaviour\",\"Data\":{\"BehaviourMeta\":{\"BehaviourId\":4}}}", behaviourMetaPath);
        await _project.Resources.Write("{\"AssetId\":\"texture\",\"Data\":{}}", ordinaryMetaPath);

        var metas = await _utility.GetAllBehaviourMetas();

        Assert.Equal(behaviourMetaPath, Assert.Single(metas).FullPath);
    }

    /// <summary>Writes source text under isolated root and returns normalized path.</summary>
    private static string WriteHeader(string path, string source)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, source);
        return path;
    }

    /// <summary>Deletes isolated behaviour roots.</summary>
    public void Dispose()
    {
        _project.Dispose();
        _engineDirectory.Dispose();
    }
}
