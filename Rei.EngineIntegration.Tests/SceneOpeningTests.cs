using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "SceneOpening")]
[Trait("Suite", "Lifecycle")]
public sealed class SceneOpeningTests(ITestOutputHelper output)
{
    private const string SCENE_ID = "3ef23822-5abf-47e8-bbf8-31bb3063a441";
    private const string PROFILE_ID = "e10c6503-f6d9-4e80-a341-bfca9313ee02";
    private const string PROBE_ID = "e10c6503-f6d9-4e80-a341-bfca9313ee03";

    [EngineFact]
    public async Task SelectedSceneOutsideBuildListLoadsInNativeEditorAndSurvivesPlayStopAndRestart()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        var scenes = Path.Combine(engine.ProjectDirectory, "Project", "Scenes");
        var scene = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(scenes, "New Scene.scene")))!;
        scene["Name"] = "Selected Scene";
        var camera = scene["Entities"]!.AsArray().Single(e => e!["Id"]!.GetValue<int>() == 1)!;
        camera["Name"] = "Selected Camera";
        var cameraBehaviour = camera["Behaviours"]!.AsArray().Single(b => b!["Id"]!.GetValue<int>() == 8)!;
        cameraBehaviour["SerializedData"]!["_rendererSettings"]!["Value"]!["Id"]!["Value"] = PROFILE_ID;
        var selectedPath = Path.Combine(scenes, "Selected Scene.scene");
        await File.WriteAllTextAsync(selectedPath, scene.ToJsonString());
        await File.WriteAllTextAsync(selectedPath + ".meta", new JsonObject { ["AssetId"] = SCENE_ID, ["Data"] = new JsonObject() }.ToJsonString());
        var projectPath = Directory.GetFiles(engine.ProjectDirectory, "*.rei").Single();
        var project = JsonNode.Parse(await File.ReadAllTextAsync(projectPath))!;
        project["LastSceneId"] = SCENE_ID;
        await File.WriteAllTextAsync(projectPath, project.ToJsonString());
        var buildPath = Path.Combine(engine.ProjectDirectory, "Project", "Settings", "Build", "Build Scenes Configuration.asset");
        var originalBuildSettings = await File.ReadAllTextAsync(buildPath);
        Assert.DoesNotContain(SCENE_ID, originalBuildSettings);

        await engine.StartAsync();
        await AssertSelectedNativeEditorAsync(engine);
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await engine.WaitUntilAsync(async () =>
        {
            var native = await engine.ReadAssetAsync(PROBE_ID, "runtime");
            if (native.GetProperty("status").GetString() != "loaded") return false;
            var values = native.GetProperty("values");
            return values.GetProperty("Samples").GetInt32() > 0 && values.GetProperty("ProfileId").GetString() == PROFILE_ID &&
                Math.Abs(values.GetProperty("ExposureEV").GetDouble() + 2) < 0.00001;
        });
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await AssertSelectedNativeEditorAsync(engine);
        await engine.CallAsync("rei_editor_save_project");
        Assert.Equal(SCENE_ID, JsonNode.Parse(await File.ReadAllTextAsync(projectPath))!["LastSceneId"]!.GetValue<string>());
        Assert.Equal(originalBuildSettings, await File.ReadAllTextAsync(buildPath));
        await engine.RestartAsync();
        await AssertSelectedNativeEditorAsync(engine);
        Assert.Equal(originalBuildSettings, await File.ReadAllTextAsync(buildPath));
    }

    private static async Task AssertSelectedNativeEditorAsync(EngineIntegrationHarness engine)
    {
        var entity = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 1 });
        Assert.Equal("Selected Camera", entity.GetProperty("name").GetString());
        var camera = entity.GetProperty("behaviours").EnumerateArray().Single(b => b.GetProperty("name").GetString() == "Camera");
        var field = camera.GetProperty("properties").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "_rendererSettings");
        Assert.Equal(PROFILE_ID, field.GetProperty("value").GetProperty("Id").GetString());
        // Native asset inspection reads engine state, independently of Editor scene properties.
        await engine.WaitUntilAsync(async () => (await engine.ReadAssetAsync(PROFILE_ID, "runtime")).GetProperty("status").GetString() == "loaded");
        var native = await engine.ReadAssetAsync(PROFILE_ID, "runtime");
        Assert.InRange(Math.Abs(native.GetProperty("values").GetProperty("_exposure").GetDouble() + 2), 0, 0.00001);
    }
}
