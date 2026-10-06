using System.Text.Json;
using Xunit.Abstractions;
using static Rei.EngineIntegration.Tests.AssetAssertions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "HDR")]
[Trait("Suite", "Lifecycle")]
public sealed class RendererSettingsTests(ITestOutputHelper output)
{
    private const string PROFILE_ID = "e10c6503-f6d9-4e80-a341-bfca9313ee01";
    private const string ALTERNATE_PROFILE_ID = "e10c6503-f6d9-4e80-a341-bfca9313ee02";
    private const string PROBE_ID = "e10c6503-f6d9-4e80-a341-bfca9313ee03";

    [EngineFact]
    public async Task BuiltinProfileLoadsUpdatesAndChangesCameraDependencyWithIndependentNativeReadback()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await engine.WaitUntilAsync(async () => (await engine.ReadAssetAsync(PROFILE_ID, "runtime")).GetProperty("status").GetString() == "loaded");
        await AssertPropertyAsync(engine, "_exposureEV", 0.0, PROFILE_ID);
        await AssertPropertyAsync(engine, "_maxPointLights", 8, PROFILE_ID);
        Assert.Equal("unloaded", (await engine.ReadAssetAsync(ALTERNATE_PROFILE_ID, "runtime")).GetProperty("status").GetString());
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertCameraAsync(engine, PROFILE_ID, 0, 1);
        await SetAsync(engine, "_exposureEV", 1.25, PROFILE_ID);
        await AssertPropertyAsync(engine, "_exposureEV", 1.25, PROFILE_ID);
        await SetAsync(engine, "_toneMapping", 0, PROFILE_ID);
        await AssertPropertyAsync(engine, "_toneMapping", 0, PROFILE_ID);
        await SetAsync(engine, "_maxPointLights", 3, PROFILE_ID);
        await AssertPropertyAsync(engine, "_maxPointLights", 3, PROFILE_ID);
        await AssertCameraAsync(engine, PROFILE_ID, 1.25, 0, 3);

        var unloadedWrite = await SetAsync(engine, "_exposureEV", -3, ALTERNATE_PROFILE_ID);
        Assert.False(unloadedWrite.GetProperty("runtimeSynced").GetBoolean());
        Assert.Equal("unloaded", (await engine.ReadAssetAsync(ALTERNATE_PROFILE_ID, "runtime")).GetProperty("status").GetString());
        await SetCameraProfileAsync(engine, ALTERNATE_PROFILE_ID);
        // An unloaded asset loads from its imported snapshot. No implicit loading/reimport on edit.
        await AssertCameraAsync(engine, ALTERNATE_PROFILE_ID, -2, 0);
        await SetAsync(engine, "_exposureEV", -3, ALTERNATE_PROFILE_ID);
        await AssertCameraAsync(engine, ALTERNATE_PROFILE_ID, -3, 0);
        await AssertPropertyAsync(engine, "_exposureEV", -3, ALTERNATE_PROFILE_ID);
        await SetCameraProfileAsync(engine, "");
        await AssertCameraAsync(engine, "", 0, 0);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForProfileAsync(engine);
        await AssertPropertyAsync(engine, "_exposureEV", 0, PROFILE_ID);
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertCameraAsync(engine, PROFILE_ID, 0, 1);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForProfileAsync(engine);
    }

    [EngineFact]
    public async Task SavedProfileSurvivesDllReloadPlayStopAndRestart()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await engine.WaitUntilAsync(async () => (await engine.ReadAssetAsync(PROFILE_ID, "runtime")).GetProperty("status").GetString() == "loaded");
        await SetAsync(engine, "_exposureEV", -0.75, PROFILE_ID);
        await SetAsync(engine, "_maxPointLights", 6, PROFILE_ID);
        await AssertPropertyAsync(engine, "_exposureEV", -0.75, PROFILE_ID);
        await AssertPropertyAsync(engine, "_maxPointLights", 6, PROFILE_ID);
        await engine.CallAsync("rei_editor_save_project");
        var path = Path.Combine(engine.ProjectDirectory, "Project", "DataAssets", "Tests", "RendererSettings.asset");
        using (var saved = JsonDocument.Parse(await File.ReadAllTextAsync(path)))
        {
            Assert.Equal(-0.75, saved.RootElement.GetProperty("SerializedData").GetProperty("_exposureEV").GetProperty("Value").GetDouble());
            Assert.Equal(6, saved.RootElement.GetProperty("SerializedData").GetProperty("_maxPointLights").GetProperty("Value").GetInt32());
        }
        await engine.RunOperationAsync("rei_editor_start_build", new() { ["configuration"] = "editor_debug", ["forceSolutionRebuild"] = true });
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertCameraAsync(engine, PROFILE_ID, -0.75, 1, 6);
        await SetAsync(engine, "_exposureEV", 2, PROFILE_ID);
        await SetAsync(engine, "_maxPointLights", 2, PROFILE_ID);
        await AssertCameraAsync(engine, PROFILE_ID, 2, 1, 2);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForProfileAsync(engine);
        await AssertPropertyAsync(engine, "_exposureEV", -0.75, PROFILE_ID);
        await AssertPropertyAsync(engine, "_maxPointLights", 6, PROFILE_ID);
        await engine.RestartAsync();
        await WaitForProfileAsync(engine);
        await AssertPropertyAsync(engine, "_exposureEV", -0.75, PROFILE_ID);
        await AssertPropertyAsync(engine, "_maxPointLights", 6, PROFILE_ID);
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertCameraAsync(engine, PROFILE_ID, -0.75, 1, 6);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForProfileAsync(engine);
    }

    [EngineFact]
    public async Task ProjectDefaultIsCreatedAndAppliedToNewCameraPersistsAndRecoversAfterDeletion()
    {
        const string DEFAULT_ID = "REI_DEFAULT_RENDERER_SETTINGS";
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        var path = Path.Combine(engine.ProjectDirectory, "Project", "Settings", "Rendering", "Renderer Settings.asset");
        Assert.True(File.Exists(path));
        using (var meta = JsonDocument.Parse(await File.ReadAllTextAsync(path + ".meta")))
            Assert.Equal(DEFAULT_ID, meta.RootElement.GetProperty("AssetId").GetString());
        await engine.CallAsync("rei_editor_add_behaviour", new() { ["entityId"] = 2, ["behaviourName"] = "Camera" });
        var entity = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 });
        Assert.Equal(DEFAULT_ID, CameraProfileId(entity));
        var originalCamera = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 1 });
        Assert.Equal(PROFILE_ID, CameraProfileId(originalCamera));
        await engine.WaitUntilAsync(async () => (await engine.ReadAssetAsync(DEFAULT_ID, "runtime")).GetProperty("status").GetString() == "loaded");
        await AssertPropertyAsync(engine, "_exposureEV", 0, DEFAULT_ID);
        await AssertPropertyAsync(engine, "_toneMapping", 1, DEFAULT_ID);
        await AssertPropertyAsync(engine, "_maxPointLights", 8, DEFAULT_ID);
        await SetAsync(engine, "_exposureEV", -0.5, DEFAULT_ID);
        await AssertPropertyAsync(engine, "_exposureEV", -0.5, DEFAULT_ID);
        await engine.CallAsync("rei_editor_set_behaviour_property", new()
        {
            ["entityId"] = 1, ["behaviourName"] = "HdrProbe", ["propertyName"] = "_camera",
            ["value"] = new Dictionary<string, object?> { ["SceneEntityId"] = 2 }
        });
        await engine.CallAsync("rei_editor_save_project");
        using (var disk = JsonDocument.Parse(await File.ReadAllTextAsync(path)))
            Assert.Equal(-0.5, disk.RootElement.GetProperty("SerializedData").GetProperty("_exposureEV").GetProperty("Value").GetDouble());
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertCameraAsync(engine, DEFAULT_ID, -0.5, 1);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await engine.RestartAsync();
        await engine.WaitUntilAsync(async () => (await engine.ReadAssetAsync(DEFAULT_ID, "runtime")).GetProperty("status").GetString() == "loaded");
        await AssertPropertyAsync(engine, "_exposureEV", -0.5, DEFAULT_ID);
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertCameraAsync(engine, DEFAULT_ID, -0.5, 1);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await engine.RestartAsync(_ =>
        {
            File.Delete(path);
            File.Delete(path + ".meta");
            return Task.CompletedTask;
        });
        await engine.WaitUntilAsync(async () => (await engine.ReadAssetAsync(DEFAULT_ID, "runtime")).GetProperty("status").GetString() == "loaded");
        await AssertPropertyAsync(engine, "_exposureEV", 0, DEFAULT_ID);
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertCameraAsync(engine, DEFAULT_ID, 0, 1);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
    }

    private static string? CameraProfileId(JsonElement entity) => entity.GetProperty("behaviours").EnumerateArray()
        .Single(b => b.GetProperty("name").GetString() == "Camera").GetProperty("properties").EnumerateArray()
        .Single(p => p.GetProperty("name").GetString() == "_rendererSettings").GetProperty("value").GetProperty("Id").GetString();

    private static Task WaitForProfileAsync(EngineIntegrationHarness engine) =>
        engine.WaitUntilAsync(async () => (await engine.ReadAssetAsync(PROFILE_ID, "runtime")).GetProperty("status").GetString() == "loaded");

    private static Task<JsonElement> SetCameraProfileAsync(EngineIntegrationHarness engine, string id) => engine.CallAsync("rei_editor_set_behaviour_property", new()
    {
        ["entityId"] = 1, ["behaviourName"] = "Camera", ["propertyName"] = "_rendererSettings", ["value"] = new Dictionary<string, object?> { ["Id"] = id }
    });

    private static async Task AssertCameraAsync(EngineIntegrationHarness engine, string id, double exposure, int toneMapping, int maxPointLights = 8)
    {
        await engine.WaitUntilAsync(async () =>
        {
            var native = await engine.ReadAssetAsync(PROBE_ID, "runtime");
            if (native.GetProperty("status").GetString() != "loaded") return false;
            var values = native.GetProperty("values");
            return values.GetProperty("Samples").GetInt32() > 0 && values.GetProperty("ProfileId").GetString() == id &&
                Math.Abs(values.GetProperty("ExposureEV").GetDouble() - exposure) <= 0.00001 && values.GetProperty("ToneMapping").GetInt32() == toneMapping && values.GetProperty("MaxPointLights").GetInt32() == maxPointLights;
        });
    }
}
