using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "SceneSync")]
[Trait("Suite", "Lifecycle")]
public sealed class SceneSynchronizationTests(ITestOutputHelper output)
{
    private sealed record EntityState(int Id, string Name, int ParentId, int Order);
    private int _step;

    [EngineFact]
    public async Task SceneMetadataAndSelectionReachEditorAcrossPlayStopAndDllReload()
    {
        foreach (var extraEntities in new[] { 0, 100 })
        {
            await using var engine = new EngineIntegrationHarness();
            output.WriteLine($"Artifacts: {engine.RunDirectory}; extraEntities={extraEntities}");
            await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
            await PrepareProbeAsync(engine.ProjectDirectory, extraEntities);
            await engine.StartAsync();
            var initial = await CommandAsync(engine, "read");
            Assert.Equal(extraEntities + 2, States(initial).Length);
            await VerifyEditorSelectionAsync(engine);
            await VerifyEditorParentAsync(engine);
            await engine.CallAsync("rei_editor_rename_entity", new() { ["entityId"] = 2, ["newName"] = "Editor renamed" });
            var renamed = await CommandAsync(engine, "read");
            Assert.Equal("Editor renamed", States(renamed).Single(entity => entity.Id == 2).Name);
            await File.WriteAllTextAsync(CommandPath(engine), "{\"Step\":0}");
            await engine.RunOperationAsync("rei_editor_start_playmode");
            await AssertPersistedStateAsync(engine, States(renamed));
            Assert.Equal(States(renamed), States(await CommandAsync(engine, "read")));
            await VerifyEditorSelectionAsync(engine);
            await VerifyEditorParentAsync(engine);
            await VerifyNativeChangesAsync(engine);

            await File.WriteAllTextAsync(CommandPath(engine), "{\"Step\":0}");
            await engine.RunOperationAsync("rei_editor_stop_playmode");
            await engine.WaitUntilAsync(async () => (await engine.CallAsync("rei_editor_get_state")).GetProperty("engine").GetProperty("status").GetString() == "running");
            Assert.Equal(States(renamed), States(await CommandAsync(engine, "read")));
            await VerifyEditorSelectionAsync(engine);
            await VerifyEditorParentAsync(engine);
            await AssertPersistedStateAsync(engine, States(renamed));
            await VerifyNativeChangesAsync(engine);
        }
    }

    private static async Task AssertPersistedStateAsync(EngineIntegrationHarness engine, EntityState[] expected)
    {
        using var scene = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project", "Scenes", "New Scene.scene")));
        var saved = scene.RootElement.GetProperty("Entities").EnumerateArray().Select(entity =>
        {
            var transform = entity.GetProperty("Behaviours").EnumerateArray().Single(behaviour => behaviour.GetProperty("Id").GetInt32() == 2).GetProperty("SerializedData");
            return new EntityState(entity.GetProperty("Id").GetInt32(), entity.GetProperty("Name").GetString()!,
                transform.GetProperty("_parent").GetProperty("Value").GetInt32(), transform.GetProperty("_order").GetProperty("Value").GetInt32());
        }).OrderBy(entity => entity.Id).ToArray();
        Assert.Equal(expected, saved);
    }

    private async Task VerifyNativeChangesAsync(EngineIntegrationHarness engine)
    {
        var parentState = await CommandAsync(engine, "create", ("Name", "Native parent"));
        var parent = States(parentState).Single(entity => entity.Name == "Native parent").Id;
        var childState = await CommandAsync(engine, "create", ("Name", "Native child"), ("ParentId", parent));
        var child = States(childState).Single(entity => entity.Name == "Native child").Id;
        var siblingState = await CommandAsync(engine, "create", ("Name", "Native sibling"), ("ParentId", parent));
        var sibling = States(siblingState).Single(entity => entity.Name == "Native sibling").Id;
        await CommandAsync(engine, "rename", ("Id", child), ("Name", "Renamed child"));
        await CommandAsync(engine, "order", ("Id", child), ("Order", 1));
        var reordered = await CommandAsync(engine, "order", ("Id", sibling), ("Order", 0));
        Assert.True(States(reordered).Single(entity => entity.Id == sibling).Order < States(reordered).Single(entity => entity.Id == child).Order);
        await CommandAsync(engine, "select", ("Ids", new[] { child, sibling }));
        await CommandAsync(engine, "parent", ("Id", child), ("ParentId", 0), ("Order", 0));
        var removed = await CommandAsync(engine, "destroy", ("Id", parent));
        Assert.Contains(States(removed), entity => entity.Id == child && entity.ParentId == 0);
        Assert.DoesNotContain(States(removed), entity => entity.Id == parent || entity.Id == sibling);
        await CommandAsync(engine, "burst", ("Id", child));
        await CommandAsync(engine, "select", ("Ids", Array.Empty<int>()));
        Assert.Empty(States(await CommandAsync(engine, "clear")));
    }

    private async Task VerifyEditorParentAsync(EngineIntegrationHarness engine)
    {
        var original = States(await CommandAsync(engine, "read")).Single(entity => entity.Id == 2);
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 2 } });
        foreach (var (parent, order) in new[] { (1, 0), (0, 0), (original.ParentId, original.Order) })
        {
            var accepted = await engine.CallAsync("rei_editor_set_entity_parent", new() { ["entityId"] = 2, ["parentId"] = parent, ["order"] = order });
            Assert.True(accepted.GetProperty("accepted").GetBoolean());
            var native = await ReadUntilAsync(engine, packet => States(packet).Any(entity => entity.Id == 2 && entity.ParentId == parent && entity.Order == order));
            var moved = States(native).Single(entity => entity.Id == 2);
            Assert.Equal(parent, moved.ParentId);
            Assert.Equal(order, moved.Order);
            Assert.Equal(new[] { 2 }, native.GetProperty("SelectedIds").EnumerateArray().Select(id => id.GetInt32()));
            await AssertSelectionAsync(engine, new[] { 2 });
        }
    }

    private static async Task VerifyEditorSelectionAsync(EngineIntegrationHarness engine)
    {
        foreach (var ids in new[] { new[] { 1 }, new[] { 2 }, new[] { 1, 2, 1 }, Array.Empty<int>() })
        {
            await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = ids });
            await AssertSelectionAsync(engine, ids.Distinct().Order().ToArray());
        }
    }

    internal async Task<JsonElement> CommandAsync(EngineIntegrationHarness engine, string action, params (string Key, object Value)[] fields)
    {
        var native = await ReadNativeCommandAsync(engine, action, fields);
        await AssertEditorMatchesNativeAsync(engine, native);
        return native;
    }

    internal async Task<JsonElement> ReadUntilAsync(EngineIntegrationHarness engine, Func<JsonElement, bool> condition)
    {
        JsonElement native = default;
        // Editor commands enqueue native work. A probe can run before that work in the same frame.
        await engine.WaitUntilAsync(async () =>
        {
            native = await ReadNativeCommandAsync(engine, "read");
            return condition(native);
        }, TimeSpan.FromSeconds(20));
        await AssertEditorMatchesNativeAsync(engine, native);
        return native;
    }

    private async Task<JsonElement> ReadNativeCommandAsync(EngineIntegrationHarness engine, string action, params (string Key, object Value)[] fields)
    {
        var step = ++_step;
        var command = fields.ToDictionary(field => field.Key, field => field.Value);
        command["Step"] = step;
        command["Action"] = action;
        await File.WriteAllTextAsync(CommandPath(engine), JsonSerializer.Serialize(command));
        JsonElement native = default;
        await engine.WaitUntilAsync(async () =>
        {
            try
            {
                using var packet = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "scene-sync-state.json")));
                if (packet.RootElement.GetProperty("Step").GetInt32() == step) native = packet.RootElement.Clone();
            }
            catch (IOException) { }
            catch (JsonException) { }
            return native.ValueKind != JsonValueKind.Undefined;
        }, TimeSpan.FromSeconds(20));

        return native;
    }

    private static async Task AssertEditorMatchesNativeAsync(EngineIntegrationHarness engine, JsonElement native)
    {
        // Probe reads native ECS fields directly; list_entities reads Editor hierarchy only.
        var expected = States(native);
        await engine.WaitUntilAsync(async () =>
        {
            var editor = await engine.CallAsync("rei_editor_list_entities");
            var rows = editor.GetProperty("entities").EnumerateArray().ToArray();
            // Validate the actual tree traversal too; Transform metadata alone can hide stale nodes.
            var ancestors = new List<int>();
            var lastOrderByParent = new Dictionary<int, int>();
            foreach (var row in rows)
            {
                var depth = row.GetProperty("depth").GetInt32();
                if (depth < 0 || depth > ancestors.Count) return false;
                ancestors.RemoveRange(depth, ancestors.Count - depth);
                var parent = depth == 0 ? 0 : ancestors[depth - 1];
                if (row.GetProperty("parentId").GetInt32() != parent) return false;
                var order = row.GetProperty("order").GetInt32();
                if (lastOrderByParent.TryGetValue(parent, out var previousOrder) && previousOrder > order) return false;
                lastOrderByParent[parent] = order;
                ancestors.Add(row.GetProperty("id").GetInt32());
            }
            var actual = rows.Select(entity => new EntityState(
                entity.GetProperty("id").GetInt32(), entity.GetProperty("name").GetString()!,
                entity.GetProperty("parentId").GetInt32(), entity.GetProperty("order").GetInt32())).OrderBy(entity => entity.Id).ToArray();
            return expected.SequenceEqual(actual);
        }, TimeSpan.FromSeconds(15));
        await AssertSelectionAsync(engine, native.GetProperty("SelectedIds").EnumerateArray().Select(id => id.GetInt32()).Order().ToArray());
    }

    private static async Task AssertSelectionAsync(EngineIntegrationHarness engine, int[] expected)
    {
        await engine.WaitUntilAsync(async () =>
        {
            var editor = await engine.CallAsync("rei_editor_get_selection_state", new() { ["source"] = "editor" });
            var runtime = await engine.CallAsync("rei_editor_get_selection_state", new() { ["source"] = "runtime" });
            return editor.GetProperty("status").GetString() == "ok" && runtime.GetProperty("status").GetString() == "ok" &&
                expected.SequenceEqual(Ids(editor)) && expected.SequenceEqual(Ids(runtime));
        }, TimeSpan.FromSeconds(15));
    }

    private static int[] Ids(JsonElement packet) => packet.GetProperty("selectedIds").EnumerateArray().Select(id => id.GetInt32()).Order().ToArray();
    private static EntityState[] States(JsonElement packet) => packet.GetProperty("Entities").EnumerateArray().Select(entity => new EntityState(
        entity.GetProperty("Id").GetInt32(), entity.GetProperty("Name").GetString()!,
        entity.GetProperty("ParentId").GetInt32(), entity.GetProperty("Order").GetInt32())).OrderBy(entity => entity.Id).ToArray();
    private static string CommandPath(EngineIntegrationHarness engine) => Path.Combine(engine.ProjectDirectory, "scene-sync-command.json");

    internal static async Task PrepareProbeAsync(string project, int extraEntities, bool preserveApplication = false)
    {
        if (extraEntities > 0)
        {
            var scenePath = Path.Combine(project, "Project", "Scenes", "New Scene.scene");
            var scene = JsonNode.Parse(await File.ReadAllTextAsync(scenePath))!;
            var entities = scene["Entities"]!.AsArray();
            var transform = entities[0]!["Behaviours"]!.AsArray().Single(behaviour => behaviour!["Id"]!.GetValue<int>() == 2)!;
            var nextId = entities.Max(entity => entity!["Id"]!.GetValue<int>()) + 1;
            for (var i = 0; i < extraEntities; i++)
                entities.Add(new JsonObject
                {
                    ["Id"] = nextId + i, ["Name"] = $"Scene load {i}",
                    ["Behaviours"] = new JsonArray(transform.DeepClone())
                });
            await File.WriteAllTextAsync(scenePath, scene.ToJsonString());
        }
        var source = """
#include "Startup/AppEntryPoint.h"
#include "Modules/Editor/EntitySelectionUtility.h"
#include <fstream>

class SceneSyncProbe final
{
    i32 _step = 0;
    i32 _burst = 0;
    rei::ecs::Entity _burstEntity = rei::ecs::NULL_ENTITY;
public:
    void Tick()
    {
        const auto world = rei::GetInternalWorld();
        ECS_WORLD(world);
        if (_burst > 0)
        {
            GET(_burstEntity, EntityInfo).Name = "burst-" + std::to_string(65 - _burst);
            if (--_burst > 0) return;
            Publish();
            return;
        }
        std::ifstream file(R"(__COMMAND_PATH__)");
        if (!file) return;
        const auto command = nlohmann::json::parse(file, nullptr, false);
        if (command.is_discarded() || !command.contains("Step")) return;
        const i32 step = command.at("Step");
        if (step <= _step) return;
        _step = step;
        Apply(command);
    }

    void Apply(const nlohmann::json& command)
    {
        const auto world = rei::GetInternalWorld();
        ECS_WORLD(world);
        const std::string action = command.at("Action");
        const auto entity = rei::GetEntityManager().GetBySceneId(command.value("Id", 0));
        const auto parent = rei::GetEntityManager().GetBySceneId(command.value("ParentId", 0));
        if (action == "create")
        {
            const auto created = rei::GetEntityManager().CreateNewEntity(command.at("Name").get<std::string>());
            GET(created, rei::Transform).SetParent(parent, 0);
        }
        else if (action == "rename") GET(entity, EntityInfo).Name = command.at("Name").get<std::string>();
        else if (action == "parent") GET(entity, rei::Transform).SetParent(parent, command.value("Order", 0));
        else if (action == "order") GET(entity, rei::Transform).SetChildOrder(command.at("Order"));
        else if (action == "destroy") rei::GetEntityManager().Destroy(entity);
        else if (action == "select")
        {
            rei::editor::selection_utility::Reset(world);
            for (const i32 id : command.at("Ids"))
                rei::editor::selection_utility::Select(world, rei::GetEntityManager().GetBySceneId(id), false);
        }
        else if (action == "clear")
        {
            const auto roots = rei::GetEntityManager().GetRootEntities();
            for (const auto root : roots) rei::GetEntityManager().Destroy(root);
        }
        else if (action == "burst")
        {
            _burst = 64;
            _burstEntity = entity;
            return;
        }
        world->Refresh();
        Publish();
    }

    void Publish()
    {
        ECS_WORLD(rei::GetInternalWorld());
        auto entities = nlohmann::json::array();
        std::vector<i32> selected;
        const auto& filter = FILTER(EntityInfo);
        FOR(entity, filter)
        {
            const auto& info = GET(entity, EntityInfo);
            const auto& transform = GET(entity, rei::Transform);
            const auto parent = transform.GetParent();
            const i32 parentId = !IS_DEAD(parent) && HAS(parent, EntityInfo) ? GET(parent, EntityInfo).Id : 0;
            entities.push_back({{"Id", info.Id}, {"Name", info.Name}, {"ParentId", parentId}, {"Order", transform.GetChildOrder()}});
            if (HAS(entity, rei::editor::SelectedTag)) selected.push_back(info.Id);
        }
        const nlohmann::json packet = {{"Step", _step}, {"Entities", entities}, {"SelectedIds", selected}};
        std::ofstream file(R"(__STATE_PATH__)");
        file << packet.dump();
        LOG("SceneSyncProbe step={} entities={} selected={}", _step, entities.size(), selected.size());
    }
};

class TestApplication final : public rei::App
{
    SceneSyncProbe _probe;
public:
    void OnStart() override
    {
        rei::GetInternalWorld()->AddSystem([this] { _probe.Tick(); });
    }
};
std::shared_ptr<rei::App> CreateApp() { return std::make_shared<TestApplication>(); }
""";
        source = source.Replace("__COMMAND_PATH__", Path.Combine(project, "scene-sync-command.json"))
            .Replace("__STATE_PATH__", Path.Combine(project, "scene-sync-state.json"));
        var appPath = Path.Combine(project, "Project", "Scripts", "ReiApp.cpp");
        if (preserveApplication)
        {
            var existing = (await File.ReadAllTextAsync(appPath)).Replace("\r\n", "\n");
            var probe = source[..source.IndexOf("class TestApplication", StringComparison.Ordinal)];
            var application = System.Text.RegularExpressions.Regex.Match(existing, @"class\s+\w+\s+(?:final\s+)?:\s*public\s+rei::App\s*\{");
            if (!application.Success) throw new InvalidDataException("Probe requires an application class deriving from rei::App.");
            existing = existing.Insert(application.Index + application.Length, "\n    SceneSyncProbe _sceneSyncProbe;");
            existing = existing.Insert(application.Index, probe);
            var onStart = System.Text.RegularExpressions.Regex.Match(existing, @"void\s+OnStart\(\)\s+override\s*\{");
            if (!onStart.Success) throw new InvalidDataException("Probe requires an OnStart override.");
            existing = existing.Insert(onStart.Index + onStart.Length, "\n        rei::GetInternalWorld()->AddSystem([this] { _sceneSyncProbe.Tick(); });");
            source = existing;
        }
        await File.WriteAllTextAsync(appPath, source);
    }
}
