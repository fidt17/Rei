using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Lifecycle")]
[Trait("Area", "SceneSwitching")]
public sealed class SceneSwitchingTests(ITestOutputHelper output)
{
    private const string SCENE_B = "5cfba0ba-cb80-4785-bdc0-95506164cedc";
    private const string SCENE_BAD = "60fdbeb9-6cbf-4e23-8cf6-0252a626eadb";
    private const string NEW_CONFIG = "7c79afc1-b558-4cdd-a278-ab8f43107ed5";
    private int _step;

    [EngineFact]
    public async Task EditorSwitchesLiveScenesWithFreshDataDependenciesAndFailureRecovery()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        await PrepareAsync(engine.ProjectDirectory);
        await engine.StartAsync();
        var initial = await ReadNativeAsync(engine);
        var initialProfile = await ProfileAsync(engine);
        var originalScene = (await engine.CallAsync("rei_editor_get_state")).GetProperty("scene").GetProperty("id").GetString()!;
        await AssertStartupConfigurationAsync(engine, originalScene);
        var persistedB = await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project/Scenes/Other.scene"));

        // This dependency did not exist in the resource pack loaded at engine startup.
        await WriteNewConfigAsync(engine.ProjectDirectory);
        await engine.RunOperationAsync("rei_editor_refresh_assets");
        // Reimport has its own staged rebuild/restart. Measure scene transitions after it finishes.
        await engine.WaitUntilAsync(async () =>
        {
            var state = await engine.CallAsync("rei_editor_get_state");
            if (state.GetProperty("engine").GetProperty("status").GetString() != "running" ||
                state.GetProperty("automation").GetProperty("isBuilding").GetBoolean() ||
                state.GetProperty("automation").GetProperty("isImporting").GetBoolean()) return false;
            var profile = await ProfileAsync(engine);
            return profile.TryGetProperty("sessionId", out var session) && session.GetString() != initialProfile.GetProperty("sessionId").GetString();
        }, TimeSpan.FromMinutes(2));
        initial = await ReadNativeAsync(engine);
        initialProfile = await ProfileAsync(engine);
        persistedB = await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project/Scenes/Other.scene"));
        Assert.Equal("unloaded", (await engine.ReadAssetAsync(NEW_CONFIG, "runtime")).GetProperty("status").GetString());
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 1 } });
        await engine.RunOperationAsync("rei_editor_open_scene", new() { ["scene"] = "Scenes/Other.scene" });
        var current = await ReadNativeAsync(engine);
        await AssertSceneAsync(engine, current, "B parent", 11.5);
        await AssertGameSceneConfigurationAsync(engine, originalScene);
        AssertIdentity(initial, current);
        Assert.Equal(initialProfile.GetProperty("sessionId").GetString(), (await ProfileAsync(engine)).GetProperty("sessionId").GetString());
        var dependency = await engine.ReadAssetAsync(NEW_CONFIG, "runtime");
        Assert.Equal("loaded", dependency.GetProperty("status").GetString());
        Assert.InRange(Math.Abs(dependency.GetProperty("values").GetProperty("_floatValue").GetDouble() - 82.25), 0, 0.0001);
        foreach (var source in new[] { "editor", "runtime" })
            Assert.Empty((await engine.CallAsync("rei_editor_get_selection_state", new() { ["source"] = source })).GetProperty("selectedIds").EnumerateArray());

        await engine.CallAsync("rei_editor_set_behaviour_property", new()
        {
            ["entityId"] = 1, ["behaviourName"] = "Transform", ["propertyName"] = "_position",
            ["value"] = new { x = 42.125, y = 0.0, z = 0.0 }
        });
        await engine.WaitUntilAsync(async () =>
        {
            current = await ReadNativeAsync(engine);
            return Math.Abs(current.GetProperty("Entities")[0].GetProperty("X").GetDouble() - 42.125) < 0.0001;
        });
        await AssertSceneAsync(engine, current, "B parent", 42.125);
        await engine.RunOperationAsync("rei_editor_open_scene", new() { ["scene"] = originalScene });
        Assert.Equal("unloaded", (await engine.ReadAssetAsync(NEW_CONFIG, "runtime")).GetProperty("status").GetString());
        await engine.RunOperationAsync("rei_editor_open_scene", new() { ["scene"] = SCENE_B });
        current = await ReadNativeAsync(engine);
        await AssertSceneAsync(engine, current, "B parent", 42.125);
        var oldHandle = current.GetProperty("Entities")[0].GetProperty("Handle").GetString();
        await engine.RunOperationAsync("rei_editor_open_scene", new() { ["scene"] = SCENE_B });
        current = await ReadNativeAsync(engine);
        Assert.NotEqual(oldHandle, current.GetProperty("Entities")[0].GetProperty("Handle").GetString());
        AssertIdentity(initial, current);
        Assert.Equal(persistedB, await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project/Scenes/Other.scene")));

        var failed = await engine.CallAsync("rei_editor_open_scene", new() { ["scene"] = SCENE_BAD });
        await engine.WaitUntilAsync(async () =>
            (await engine.CallAsync("rei_editor_get_operation", new() { ["operationId"] = failed.GetProperty("id").GetString() })).GetProperty("status").GetString() == "failed");
        var empty = await ReadNativeAsync(engine);
        Assert.Empty(empty.GetProperty("Entities").EnumerateArray());
        Assert.Equal("", empty.GetProperty("SceneAssetId").GetString());
        Assert.Equal(JsonValueKind.Null, (await engine.CallAsync("rei_editor_get_state")).GetProperty("scene").ValueKind);
        Assert.Equal("unloaded", (await engine.ReadAssetAsync(NEW_CONFIG, "runtime")).GetProperty("status").GetString());
        AssertIdentity(initial, empty);
        await engine.RunOperationAsync("rei_editor_open_scene", new() { ["scene"] = SCENE_B });
        await AssertSceneAsync(engine, await ReadNativeAsync(engine), "B parent", 42.125);
        Assert.Equal(initialProfile.GetProperty("sessionId").GetString(), (await ProfileAsync(engine)).GetProperty("sessionId").GetString());

        await ResetCommandAsync(engine);
        await engine.RunOperationAsync("rei_editor_start_playmode");
        current = await ReadNativeAsync(engine);
        await AssertSceneAsync(engine, current, "B parent", 42.125);
        Assert.Equal(SCENE_B, current.GetProperty("StartupSceneId").GetString());
        var playing = current;
        await engine.RunOperationAsync("rei_editor_open_scene", new() { ["scene"] = originalScene });
        await engine.RunOperationAsync("rei_editor_open_scene", new() { ["scene"] = SCENE_B });
        current = await ReadNativeAsync(engine);
        await AssertSceneAsync(engine, current, "B parent", 42.125);
        AssertIdentity(playing, current);

        await ResetCommandAsync(engine);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForEditorAsync(engine);
        current = await ReadNativeAsync(engine);
        await AssertSceneAsync(engine, current, "B parent", 42.125);
        Assert.Equal(SCENE_B, current.GetProperty("StartupSceneId").GetString());

        var beforeReload = (await ProfileAsync(engine)).GetProperty("sessionId").GetString();
        await ResetCommandAsync(engine);
        await File.AppendAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project/Scripts/ReiApp.cpp"), "\n// Force project DLL reload through normal import pipeline.\n");
        await engine.RunOperationAsync("rei_editor_refresh_assets");
        await engine.WaitUntilAsync(async () =>
        {
            var state = await engine.CallAsync("rei_editor_get_state");
            if (state.GetProperty("engine").GetProperty("status").GetString() != "running" ||
                state.GetProperty("automation").GetProperty("isBuilding").GetBoolean()) return false;
            var profile = await ProfileAsync(engine);
            return profile.TryGetProperty("sessionId", out var session) && session.GetString() != beforeReload;
        }, TimeSpan.FromMinutes(2));
        current = await ReadNativeAsync(engine);
        await AssertSceneAsync(engine, current, "B parent", 42.125);
        Assert.Equal(SCENE_B, current.GetProperty("StartupSceneId").GetString());
        await AssertStartupConfigurationAsync(engine, originalScene);

        await ResetCommandAsync(engine);
        await engine.RestartAsync(async project =>
        {
            var path = Directory.GetFiles(project, "*.rei").Single();
            var settings = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            settings["LastSceneId"] = originalScene;
            await File.WriteAllTextAsync(path, settings.ToJsonString());
        });
        current = await ReadNativeAsync(engine);
        Assert.Equal(originalScene, current.GetProperty("SceneAssetId").GetString());
        Assert.Equal(originalScene, current.GetProperty("StartupSceneId").GetString());
        Assert.Equal(originalScene, (await engine.CallAsync("rei_editor_get_state")).GetProperty("scene").GetProperty("id").GetString());
        await AssertStartupConfigurationAsync(engine, originalScene);
    }

    [EngineFact]
    public async Task NativePlaymodeTransitionsSynchronizeEditorAndStopRestoresSelectedScene()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        await PrepareAsync(engine.ProjectDirectory);
        await WriteNewConfigAsync(engine.ProjectDirectory);
        var configPath = Path.Combine(engine.ProjectDirectory, "Project/Settings/Build/Build Scenes Configuration.asset");
        var config = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        config["Scenes"]!["1"] = SCENE_B;
        config["Scenes"]!["2"] = SCENE_BAD;
        await File.WriteAllTextAsync(configPath, config.ToJsonString());
        await engine.StartAsync();
        var selectedScene = (await engine.CallAsync("rei_editor_get_state")).GetProperty("scene").GetProperty("id").GetString()!;
        await engine.RunOperationAsync("rei_editor_start_playmode");
        var initial = await ReadNativeAsync(engine);
        var profile = await ProfileAsync(engine);
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 1 } });
        var current = await ReadNativeAsync(engine, 1);
        await AssertSceneAsync(engine, current, "B parent", 11.5);
        AssertIdentity(initial, current);
        foreach (var source in new[] { "editor", "runtime" })
            Assert.Empty((await engine.CallAsync("rei_editor_get_selection_state", new() { ["source"] = source })).GetProperty("selectedIds").EnumerateArray());
        await engine.CallAsync("rei_editor_set_behaviour_property", new()
        {
            ["entityId"] = 1, ["behaviourName"] = "Transform", ["propertyName"] = "_position",
            ["value"] = new { x = 24.75, y = 0.0, z = 0.0 }
        });
        await engine.WaitUntilAsync(async () =>
            Math.Abs((await ReadNativeAsync(engine)).GetProperty("Entities")[0].GetProperty("X").GetDouble() - 24.75) < 0.0001);
        await AssertSceneAsync(engine, await ReadNativeAsync(engine), "B parent", 24.75);
        var empty = await ReadNativeAsync(engine, 2);
        Assert.Empty(empty.GetProperty("Entities").EnumerateArray());
        Assert.Equal("", empty.GetProperty("SceneAssetId").GetString());
        await engine.WaitUntilAsync(async () => (await engine.CallAsync("rei_editor_get_state")).GetProperty("scene").ValueKind == JsonValueKind.Null);
        current = await ReadNativeAsync(engine, 1);
        await AssertSceneAsync(engine, current, "B parent", 11.5);
        AssertIdentity(initial, current);
        Assert.Equal(profile.GetProperty("sessionId").GetString(), (await ProfileAsync(engine)).GetProperty("sessionId").GetString());
        await ResetCommandAsync(engine);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForEditorAsync(engine);
        current = await ReadNativeAsync(engine);
        Assert.Equal(selectedScene, current.GetProperty("SceneAssetId").GetString());
        Assert.Equal(selectedScene, current.GetProperty("StartupSceneId").GetString());
        await engine.WaitUntilAsync(async () => (await engine.CallAsync("rei_editor_get_state")).GetProperty("scene").GetProperty("id").GetString() == selectedScene);
        await AssertStartupConfigurationAsync(engine, selectedScene);
    }

    private static async Task AssertStartupConfigurationAsync(EngineIntegrationHarness engine, string gameScene)
    {
        var projectPath = Directory.GetFiles(engine.ProjectDirectory, "*.rei").Single();
        using var project = JsonDocument.Parse(await File.ReadAllTextAsync(projectPath));
        var selected = (await engine.CallAsync("rei_editor_get_state")).GetProperty("scene").GetProperty("id").GetString();
        Assert.Equal(selected, project.RootElement.GetProperty("LastSceneId").GetString());
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project/Settings/Build/Build Scenes Configuration.asset")));
        Assert.Equal(gameScene, config.RootElement.GetProperty("Scenes").GetProperty("0").GetString());
    }

    private static async Task AssertGameSceneConfigurationAsync(EngineIntegrationHarness engine, string gameScene)
    {
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project/Settings/Build/Build Scenes Configuration.asset")));
        Assert.Equal(gameScene, config.RootElement.GetProperty("Scenes").GetProperty("0").GetString());
    }

    private static Task ResetCommandAsync(EngineIntegrationHarness engine) =>
        File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "scene-sync-command.json"), "{\"Step\":0}");

    private static Task WaitForEditorAsync(EngineIntegrationHarness engine) => engine.WaitUntilAsync(async () =>
    {
        var state = (await engine.CallAsync("rei_editor_get_state")).GetProperty("engine");
        return state.GetProperty("status").GetString() == "running" && state.GetProperty("mode").GetString() == "EditorMode";
    });

    private async Task<JsonElement> ReadNativeAsync(EngineIntegrationHarness engine, int? sceneIndex = null)
    {
        var step = ++_step;
        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "scene-sync-command.json"), JsonSerializer.Serialize(new { Step = step, Action = sceneIndex.HasValue ? "load_scene" : "read", SceneIndex = sceneIndex ?? 0 }));
        JsonElement result = default;
        await engine.WaitUntilAsync(async () =>
        {
            try
            {
                using var packet = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "scene-sync-state.json")));
                if (packet.RootElement.GetProperty("Step").GetInt32() == step) result = packet.RootElement.Clone();
            }
            catch (IOException) { }
            catch (JsonException) { }
            return result.ValueKind != JsonValueKind.Undefined;
        });
        return result;
    }

    private static Task<JsonElement> ProfileAsync(EngineIntegrationHarness engine) => engine.CallAsync("rei_editor_get_profiling_snapshot",
        new() { ["source"] = "runtime", ["view"] = "recent", ["limit"] = 1 });

    private static void AssertIdentity(JsonElement before, JsonElement after)
    {
        Assert.Equal(before.GetProperty("Engine").GetUInt64(), after.GetProperty("Engine").GetUInt64());
        Assert.Equal(before.GetProperty("Window").GetUInt64(), after.GetProperty("Window").GetUInt64());
    }

    private static async Task AssertSceneAsync(EngineIntegrationHarness engine, JsonElement native, string name, double x)
    {
        Assert.Equal(SCENE_B, native.GetProperty("SceneAssetId").GetString());
        var entities = native.GetProperty("Entities").EnumerateArray().OrderBy(entity => entity.GetProperty("Id").GetInt32()).ToArray();
        Assert.Equal(2, entities.Length);
        Assert.Equal(name, entities[0].GetProperty("Name").GetString());
        Assert.Equal(1, entities[1].GetProperty("ParentId").GetInt32());
        Assert.InRange(Math.Abs(entities[0].GetProperty("X").GetDouble() - x), 0, 0.0001);
        await engine.WaitUntilAsync(async () =>
        {
            var state = await engine.CallAsync("rei_editor_get_state");
            if (state.GetProperty("scene").GetProperty("id").GetString() != SCENE_B) return false;
            var editor = await engine.CallAsync("rei_editor_list_entities");
            var rows = editor.GetProperty("entities").EnumerateArray().ToArray();
            return rows.Length == 2 && rows[0].GetProperty("name").GetString() == name && rows[1].GetProperty("parentId").GetInt32() == 1;
        });
        var parent = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 1 });
        var transform = parent.GetProperty("behaviours").EnumerateArray().Single(behaviour => behaviour.GetProperty("name").GetString() == "Transform");
        var position = transform.GetProperty("properties").EnumerateArray().Single(property => property.GetProperty("name").GetString() == "_position");
        Assert.InRange(Math.Abs(position.GetProperty("value").GetProperty("x").GetDouble() - x), 0, 0.0001);
    }

    private static async Task WriteNewConfigAsync(string project)
    {
        var configPath = Path.Combine(project, "Project/DataAssets/Tests/NewConfig.asset");
        var config = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(project, "Project/DataAssets/Tests/DataAssetTestConfig.asset")))!;
        config["SerializedData"]!["_floatValue"]!["Value"] = 82.25;
        await File.WriteAllTextAsync(configPath, config.ToJsonString());
        var configMeta = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(project, "Project/DataAssets/Tests/DataAssetTestConfig.asset.meta")))!;
        configMeta["AssetId"] = NEW_CONFIG;
        await File.WriteAllTextAsync(configPath + ".meta", configMeta.ToJsonString());
    }

    private static async Task PrepareAsync(string project)
    {
        await SceneSynchronizationTests.PrepareProbeAsync(project, 0);
        var appPath = Path.Combine(project, "Project/Scripts/ReiApp.cpp");
        var source = await File.ReadAllTextAsync(appPath);
        source = source.Replace("#include <fstream>", "#include <fstream>\n#include <Modules/Scenes/SceneManager.h>\n#pragma comment(lib, \"opengl32.lib\")");
        source = source.Replace("{\"Id\", info.Id}, {\"Name\", info.Name}",
            "{\"Id\", info.Id}, {\"Handle\", std::string(entity)}, {\"X\", transform.GetLocalPosition().x}, {\"Name\", info.Name}");
        source = source.Replace("{\"Step\", _step}, {\"Entities\", entities}",
            "{\"Step\", _step}, {\"Engine\", reinterpret_cast<u64>(&rei::GetEngine())}, {\"Window\", reinterpret_cast<u64>(WindowFromDC(wglGetCurrentDC()))}, {\"SceneAssetId\", rei::GetSceneManager().GetActiveSceneId()}, {\"Entities\", entities}");
        source = source.Replace("if (action == \"create\")", "if (action == \"load_scene\") rei::GetSceneManager().LoadScene(command.at(\"SceneIndex\").get<i32>());\n        else if (action == \"create\")");
        source = source.Replace("    i32 _step = 0;", "    i32 _step = 0;\npublic:\n    std::string StartupSceneId;\nprivate:");
        source = source.Replace("rei::GetInternalWorld()->AddSystem([this] { _probe.Tick(); });",
            "_probe.StartupSceneId = rei::GetSceneManager().GetActiveSceneId();\n        rei::GetInternalWorld()->AddSystem([this] { _probe.Tick(); });");
        source = source.Replace("{\"Step\", _step}", "{\"Step\", _step}, {\"StartupSceneId\", StartupSceneId}");
        await File.WriteAllTextAsync(appPath, source);
        var original = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(project, "Project/Scenes/New Scene.scene")))!;
        var transform = original["Entities"]![0]!["Behaviours"]!.AsArray().Single(behaviour => behaviour!["Id"]!.GetValue<int>() == 2)!.DeepClone();
        transform["SerializedData"]!["_position"]!["Value"]!["x"]!["Value"] = 11.5;
        transform["SerializedData"]!["_parent"]!["Value"] = 0;
        var childTransform = transform.DeepClone();
        childTransform["SerializedData"]!["_parent"]!["Value"] = 1;
        var consumer = original["Entities"]!.AsArray().SelectMany(entity => entity!["Behaviours"]!.AsArray())
            .Single(behaviour => behaviour!["SerializedData"]?["_config"] != null)!.DeepClone();
        consumer["SerializedData"]!["_config"]!["Value"]!["Id"]!["Value"] = NEW_CONFIG;
        var scene = new JsonObject
        {
            ["Name"] = "Other", ["Entities"] = new JsonArray(
                new JsonObject { ["Id"] = 1, ["Name"] = "B parent", ["Behaviours"] = new JsonArray(transform, consumer) },
                new JsonObject { ["Id"] = 2, ["Name"] = "B child", ["Behaviours"] = new JsonArray(childTransform) })
        };
        await WriteSceneAsync(project, "Other", SCENE_B, scene);
        var invalid = scene.DeepClone();
        invalid["Entities"]![0]!["Behaviours"]![1]!["SerializedData"]!["_config"]!["Value"]!["Id"]!["Value"] = "missing-resource";
        await WriteSceneAsync(project, "Broken", SCENE_BAD, invalid);
    }

    private static async Task WriteSceneAsync(string project, string name, string id, JsonNode scene)
    {
        var path = Path.Combine(project, "Project/Scenes/" + name + ".scene");
        await File.WriteAllTextAsync(path, scene.ToJsonString());
        await File.WriteAllTextAsync(path + ".meta", JsonSerializer.Serialize(new { AssetId = id, Data = new { } }));
    }
}
