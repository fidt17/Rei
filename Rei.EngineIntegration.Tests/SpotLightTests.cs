using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "SpotLight")]
[Trait("Suite", "Lifecycle")]
public sealed class SpotLightTests(ITestOutputHelper output)
{
    private const string PROBE_ID = "8ac29002-6d31-49b5-b6b8-50cfa17f5801";
    private const string PROFILE_ID = "e10c6503-f6d9-4e80-a341-bfca9313ee01";
    private const double TOLERANCE = 0.00001;

    [EngineFact]
    public async Task InspectorFieldsReachNativeSpotAndSurviveSavePlayStopDllReloadAndRestart()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        var scenePath = Path.Combine(engine.ProjectDirectory, "Project", "Scenes", "New Scene.scene");
        var scene = JsonNode.Parse(await File.ReadAllTextAsync(scenePath))!;
        var target = scene["Entities"]!.AsArray().Single(e => e!["Id"]!.GetValue<int>() == 2)!;
        var position = target["Behaviours"]![0]!["SerializedData"]!["_position"]!["Value"]!;
        position["x"]!["Value"] = 3.0;
        position["y"]!["Value"] = 4.0;
        position["z"]!["Value"] = 5.0;
        await File.WriteAllTextAsync(scenePath, scene.ToJsonString());
        await engine.StartAsync();
        Assert.Equal("unloaded", (await engine.ReadAssetAsync(PROBE_ID, "runtime")).GetProperty("status").GetString());
        await engine.CallAsync("rei_editor_add_behaviour", new() { ["entityId"] = 2, ["behaviourName"] = "SpotLight" });
        await engine.CallAsync("rei_editor_add_behaviour", new() { ["entityId"] = 1, ["behaviourName"] = "SpotLightProbe" });
        await SetAsync(engine, 1, "SpotLightProbe", "_output", new Dictionary<string, object?> { ["Id"] = PROBE_ID });
        await SetAsync(engine, 1, "SpotLightProbe", "_light", new Dictionary<string, object?> { ["SceneEntityId"] = 2 });
        await SetAsync(engine, 2, "SpotLight", "_strength", 2.5);
        await SetAsync(engine, 2, "SpotLight", "_range", 6.25);
        await SetAsync(engine, 2, "SpotLight", "_innerAngle", 20.0);
        await SetAsync(engine, 2, "SpotLight", "_outerAngle", 60.0);
        await SetAsync(engine, 2, "SpotLight", "_color", new { r = 0.2, g = 0.4, b = 0.8, a = 1.0 });
        await SetAsync(engine, 2, "Transform", "_rotation", new { x = 0.0, y = 90.0, z = 0.0 });
        await SetAsync(engine, 2, "Transform", "_scale", new { x = 2.0, y = 3.0, z = 4.0 });
        await engine.CallAsync("rei_editor_set_data_asset_property", new() { ["assetId"] = PROFILE_ID, ["propertyName"] = "_maxSpotLights", ["value"] = 2 });
        await AssertEditorAsync(engine, 2.5, 6.25, 20, 60);
        await engine.CallAsync("rei_editor_save_project");
        await AssertPersistenceAsync(engine, 2.5, 6.25, 20, 60);

        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertNativeAsync(engine, 2.5, 6.25, 20, 60, 2);
        await SetAsync(engine, 2, "SpotLight", "_strength", 4.0);
        await SetAsync(engine, 2, "SpotLight", "_range", 8.5);
        await SetAsync(engine, 2, "SpotLight", "_innerAngle", 35.0);
        await SetAsync(engine, 2, "SpotLight", "_outerAngle", 75.0);
        await AssertEditorAsync(engine, 4, 8.5, 35, 75);
        await AssertNativeAsync(engine, 4, 8.5, 35, 75, 2);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await AssertEditorAsync(engine, 2.5, 6.25, 20, 60);
        await AssertPersistenceAsync(engine, 2.5, 6.25, 20, 60);

        await engine.RunOperationAsync("rei_editor_start_build", new() { ["configuration"] = "editor_debug", ["forceSolutionRebuild"] = true });
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertNativeAsync(engine, 2.5, 6.25, 20, 60, 2);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await engine.RestartAsync();
        await AssertEditorAsync(engine, 2.5, 6.25, 20, 60);
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertNativeAsync(engine, 2.5, 6.25, 20, 60, 2);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
    }

    private static Task<JsonElement> SetAsync(EngineIntegrationHarness engine, int entityId, string behaviour, string property, object value) =>
        engine.CallAsync("rei_editor_set_behaviour_property", new() { ["entityId"] = entityId, ["behaviourName"] = behaviour, ["propertyName"] = property, ["value"] = value });

    private static async Task AssertEditorAsync(EngineIntegrationHarness engine, double strength, double range, double inner, double outer)
    {
        var entity = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 });
        var fields = entity.GetProperty("behaviours").EnumerateArray().Single(b => b.GetProperty("name").GetString() == "SpotLight")
            .GetProperty("properties").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("value"));
        AssertClose(strength, fields["_strength"].GetDouble());
        AssertClose(range, fields["_range"].GetDouble());
        AssertClose(inner, fields["_innerAngle"].GetDouble());
        AssertClose(outer, fields["_outerAngle"].GetDouble());
    }

    private static async Task AssertNativeAsync(EngineIntegrationHarness engine, double strength, double range, double inner, double outer, int budget)
    {
        await engine.WaitUntilAsync(async () =>
        {
            var state = await engine.ReadAssetAsync(PROBE_ID, "runtime");
            if (state.GetProperty("status").GetString() != "loaded") return false;
            var values = state.GetProperty("values");
            return values.GetProperty("Samples").GetInt32() > 0 && Close(strength, values.GetProperty("Strength").GetDouble()) &&
                Close(range, values.GetProperty("Range").GetDouble()) && Close(inner, values.GetProperty("InnerAngle").GetDouble()) &&
                Close(outer, values.GetProperty("OuterAngle").GetDouble()) && values.GetProperty("MaxSpotLights").GetInt32() == budget;
        });
        var native = (await engine.ReadAssetAsync(PROBE_ID, "runtime")).GetProperty("values");
        var direction = native.GetProperty("Direction");
        AssertClose(1, direction.GetProperty("x").GetDouble());
        AssertClose(0, direction.GetProperty("y").GetDouble());
        AssertClose(0, direction.GetProperty("z").GetDouble());
        var position = native.GetProperty("Position");
        AssertClose(3, position.GetProperty("x").GetDouble());
        AssertClose(4, position.GetProperty("y").GetDouble());
        AssertClose(5, position.GetProperty("z").GetDouble());
        var color = native.GetProperty("Color");
        AssertClose(0.2, color.GetProperty("r").GetDouble());
        AssertClose(0.4, color.GetProperty("g").GetDouble());
        AssertClose(0.8, color.GetProperty("b").GetDouble());
        AssertClose(1, color.GetProperty("a").GetDouble());
    }

    private static async Task AssertPersistenceAsync(EngineIntegrationHarness engine, double strength, double range, double inner, double outer)
    {
        var metaPath = Path.Combine(engine.ProjectDirectory, "Internal", "rei_behaviours", "render", "light", "SpotLight.h.meta");
        using var meta = JsonDocument.Parse(await File.ReadAllTextAsync(metaPath));
        var id = meta.RootElement.GetProperty("Data").GetProperty("BehaviourMeta").GetProperty("BehaviourId").GetInt32();
        using var scene = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project", "Scenes", "New Scene.scene")));
        var light = scene.RootElement.GetProperty("Entities").EnumerateArray().Single(e => e.GetProperty("Id").GetInt32() == 2)
            .GetProperty("Behaviours").EnumerateArray().Single(b => b.GetProperty("Id").GetInt32() == id).GetProperty("SerializedData");
        AssertClose(strength, light.GetProperty("_strength").GetProperty("Value").GetDouble());
        AssertClose(range, light.GetProperty("_range").GetProperty("Value").GetDouble());
        AssertClose(inner, light.GetProperty("_innerAngle").GetProperty("Value").GetDouble());
        AssertClose(outer, light.GetProperty("_outerAngle").GetProperty("Value").GetDouble());
        using var profile = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project", "DataAssets", "Tests", "RendererSettings.asset")));
        Assert.Equal(2, profile.RootElement.GetProperty("SerializedData").GetProperty("_maxSpotLights").GetProperty("Value").GetInt32());
    }

    private static bool Close(double expected, double actual) => Math.Abs(expected - actual) <= TOLERANCE;
    private static void AssertClose(double expected, double actual) => Assert.InRange(Math.Abs(expected - actual), 0, TOLERANCE);
}
