using System.Text.Json;

namespace Rei.EngineIntegration.Tests;

public sealed class FixtureIntegrityTests
{
    [Fact]
    public void SceneBehaviourIdsHaveStableMetadata()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DataAssets");
        var behaviourIds = new HashSet<int>();
        var assetIds = new HashSet<string>();
        foreach (var path in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
        {
            using var meta = JsonDocument.Parse(File.ReadAllText(path));
            Assert.True(assetIds.Add(meta.RootElement.GetProperty("AssetId").GetString()!), $"Duplicate asset ID in {path}");
            if (meta.RootElement.GetProperty("Data").TryGetProperty("BehaviourMeta", out var behaviour))
                Assert.True(behaviourIds.Add(behaviour.GetProperty("BehaviourId").GetInt32()), $"Duplicate behaviour ID in {path}");
        }
        using var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "EngineFixture.rei")));
        Assert.Contains(project.RootElement.GetProperty("LastSceneId").GetString()!, assetIds);
        using var scene = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Project", "Scenes", "New Scene.scene")));
        foreach (var entity in scene.RootElement.GetProperty("Entities").EnumerateArray())
        foreach (var behaviour in entity.GetProperty("Behaviours").EnumerateArray())
            Assert.Contains(behaviour.GetProperty("Id").GetInt32(), behaviourIds);
    }
}
