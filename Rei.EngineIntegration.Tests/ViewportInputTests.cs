using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "ViewportInput")]
public sealed class ViewportInputTests(ITestOutputHelper output)
{
    private int _step;

    [EngineFact]
    public async Task McpDragAndKeyboardTraverseMixedHistoryWithoutSaving()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var scenePath = await PrepareAsync(engine);
        await engine.StartAsync();
        var disk = await File.ReadAllBytesAsync(scenePath);
        var entity = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 });
        var originalName = entity.GetProperty("name").GetString();
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 2 } });
        await engine.CallAsync("rei_editor_rename_entity", new() { ["entityId"] = 2, ["newName"] = "Input rename" });
        var (before, frame) = await ReadPointerAsync(engine, 2);
        var original = Position(before);
        var scaleX = frame.GetProperty("width").GetDouble() / before.GetProperty("Width").GetDouble();
        var scaleY = frame.GetProperty("height").GetDouble() / before.GetProperty("Height").GetDouble();
        var hit = before.GetProperty("Hit");
        await Drag(engine, hit[0].GetDouble() * scaleX, hit[1].GetDouble() * scaleY, 40);
        var moved = Position(await Read(engine));
        Assert.True(original.Zip(moved).Any(pair => Math.Abs(pair.First - pair.Second) > 0.01), $"No movement: {before}");
        await AssertEditorPosition(engine, moved);
        await engine.CallAsync("rei_editor_capture_frame");

        // Native keyboard callback -> Editor shortcut -> shared history -> native replay.
        await Chord(engine, 90);
        await WaitPosition(engine, original);
        await Chord(engine, 90);
        await engine.WaitUntilAsync(async () => (await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 })).GetProperty("name").GetString() == originalName);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await Chord(engine, 89);
        await engine.WaitUntilAsync(async () => (await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 })).GetProperty("name").GetString() == "Input rename");
        await Chord(engine, 89);
        await WaitPosition(engine, moved);
        Assert.Equal("Input rename", (await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 })).GetProperty("name").GetString());
        await engine.CallAsync("rei_editor_undo");
        await WaitPosition(engine, original);

        var noOp = await Read(engine);
        hit = noOp.GetProperty("Hit");
        await Drag(engine, hit[0].GetDouble() * scaleX, hit[1].GetDouble() * scaleY, 0);
        var afterNoOp = await Read(engine);
        Assert.True((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean(), $"Empty drag: before={noOp}; after={afterNoOp}");
        Assert.Equal(original, Position(afterNoOp));
        await engine.AssertToolErrorAsync("rei_editor_send_input", new() { ["events"] = new[] { new { type = "move", x = 1.0, y = 1.0 }, new { type = "move", x = 1e6, y = 0.0 } } }, "invalid_input");
        Assert.Equal(afterNoOp.GetProperty("Mouse").GetRawText(), (await Read(engine)).GetProperty("Mouse").GetRawText());
        await engine.CallAsync("rei_editor_send_input", new() { ["events"] = new object[] { new { type = "key_down", code = 87 }, new { type = "button_down" }, new { type = "scroll", y = 2 }, new { type = "wait" } } });
        var released = await Read(engine);
        Assert.False(released.GetProperty("KeyDown").GetBoolean());
        Assert.False(released.GetProperty("ButtonDown").GetBoolean());
        Assert.Equal(2, released.GetProperty("ScrollTotal").GetDouble());
        Assert.Equal(disk, await File.ReadAllBytesAsync(scenePath));
    }

    [EngineFact]
    public async Task DuplicateAndGizmoMoveReplayOnTheSameSubtree()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var scenePath = await PrepareAsync(engine, includeChild: true);
        await engine.StartAsync();
        var disk = await File.ReadAllBytesAsync(scenePath);
        var original = await ListEntitiesAsync(engine);
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 2 } });
        await Chord(engine, 68);

        JsonElement[] copies = [];
        await engine.WaitUntilAsync(async () =>
        {
            copies = (await ListEntitiesAsync(engine)).Where(entity => !original.Any(existing => EntityId(existing) == EntityId(entity))).ToArray();
            return copies.Length == 2;
        });
        var copy = EntityId(copies[0]);
        var child = EntityId(copies[1]);
        Assert.Equal(copy, copies[1].GetProperty("parentId").GetInt32());
        Assert.Equal(copy, (await Read(engine, child)).GetProperty("Parent").GetInt32());
        Assert.True(await CanUndoAsync(engine));

        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { copy } });
        var (before, frame) = await ReadPointerAsync(engine, copy);
        var initialPosition = Position(before);
        var hit = before.GetProperty("Hit");
        await Drag(engine, hit[0].GetDouble() * frame.GetProperty("width").GetDouble() / before.GetProperty("Width").GetDouble(),
            hit[1].GetDouble() * frame.GetProperty("height").GetDouble() / before.GetProperty("Height").GetDouble(), 40);
        var movedPosition = Position(await Read(engine, copy));
        Assert.False(initialPosition.SequenceEqual(movedPosition));
        await AssertEditorPosition(engine, movedPosition, copy);

        await Chord(engine, 90);
        await WaitPosition(engine, initialPosition, copy);
        await Chord(engine, 90);
        await engine.WaitUntilAsync(async () => (await ListEntitiesAsync(engine)).Select(EntityId).SequenceEqual(original.Select(EntityId)));
        Assert.Equal(JsonValueKind.Null, (await Read(engine, copy)).GetProperty("Position").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await Read(engine, child)).GetProperty("Position").ValueKind);
        Assert.False(await CanUndoAsync(engine));

        await Chord(engine, 89);
        await engine.WaitUntilAsync(async () => (await ListEntitiesAsync(engine)).Any(entity => EntityId(entity) == child && entity.GetProperty("parentId").GetInt32() == copy));
        Assert.Equal(copy, (await Read(engine, child)).GetProperty("Parent").GetInt32());
        await WaitPosition(engine, initialPosition, copy);
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { copy } });
        await Chord(engine, 89);
        await WaitPosition(engine, movedPosition, copy);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());
        Assert.Equal(disk, await File.ReadAllBytesAsync(scenePath));
    }

    [EngineFact]
    public async Task DeleteSubtreeRestoresIdsOrderAndValuesWithOneUndo()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var scenePath = await PrepareAsync(engine, includeChild: true);
        await engine.StartAsync();
        var disk = await File.ReadAllBytesAsync(scenePath);
        var before = await ListEntitiesAsync(engine);
        var child = EntityId(Assert.Single(before, entity => entity.GetProperty("parentId").GetInt32() == 2));
        var childState = await Read(engine, child);
        var expectedPosition = Position(childState);
        Assert.Equal(1.25, expectedPosition[0]);

        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 2 } });
        await engine.CallAsync("rei_editor_send_input", new()
        {
            ["events"] = new[] { new { type = "key_down", code = 261 }, new { type = "key_up", code = 261 } }
        });
        await AssertRemovedAsync();
        await engine.WaitUntilAsync(() => CanUndoAsync(engine));

        await Chord(engine, 90);
        var expectedHierarchy = before.Select(HierarchyRow).ToArray();
        await engine.WaitUntilAsync(async () => (await ListEntitiesAsync(engine)).Select(HierarchyRow).SequenceEqual(expectedHierarchy));
        var restoredChild = await Read(engine, child);
        Assert.Equal(2, restoredChild.GetProperty("Parent").GetInt32());
        Assert.Equal(childState.GetProperty("Order").GetInt32(), restoredChild.GetProperty("Order").GetInt32());
        await WaitPosition(engine, expectedPosition, child);
        Assert.False(await CanUndoAsync(engine));

        await Chord(engine, 89);
        await AssertRemovedAsync();
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());
        Assert.Equal(disk, await File.ReadAllBytesAsync(scenePath));

        async Task AssertRemovedAsync()
        {
            await engine.WaitUntilAsync(async () => (await ListEntitiesAsync(engine)).Select(EntityId).SequenceEqual(new[] { 1 }));
            Assert.Equal(JsonValueKind.Null, (await Read(engine, 2)).GetProperty("Position").ValueKind);
            Assert.Equal(JsonValueKind.Null, (await Read(engine, child)).GetProperty("Position").ValueKind);
        }
    }

    [EngineFact]
    public async Task RenameThenDeleteCanUndoBothOnTheRestoredEntity()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await PrepareAsync(engine, includeChild: true);
        await engine.StartAsync();
        var original = (await Read(engine)).GetProperty("Name").GetString();
        const string renamed = "Renamed before Delete";
        await engine.CallAsync("rei_editor_rename_entity", new() { ["entityId"] = 2, ["newName"] = renamed });
        await AssertNameAsync(renamed);

        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 2 } });
        await engine.CallAsync("rei_editor_send_input", new()
        {
            ["events"] = new[] { new { type = "key_down", code = 261 }, new { type = "key_up", code = 261 } }
        });
        await engine.WaitUntilAsync(async () => (await ListEntitiesAsync(engine)).Select(EntityId).SequenceEqual(new[] { 1 }));
        Assert.Equal(JsonValueKind.Null, (await Read(engine)).GetProperty("Name").ValueKind);

        await Chord(engine, 90);
        await engine.WaitUntilAsync(async () => (await ListEntitiesAsync(engine)).Any(entity => entity.GetProperty("parentId").GetInt32() == 2));
        await AssertNameAsync(renamed);
        await engine.WaitUntilAsync(() => CanUndoAsync(engine));
        await Chord(engine, 90);
        await AssertNameAsync(original);

        await Chord(engine, 89);
        await AssertNameAsync(renamed);
        await Chord(engine, 89);
        await engine.WaitUntilAsync(async () => (await ListEntitiesAsync(engine)).Select(EntityId).SequenceEqual(new[] { 1 }));
        Assert.Equal(JsonValueKind.Null, (await Read(engine)).GetProperty("Name").ValueKind);

        async Task AssertNameAsync(string? expected)
        {
            await engine.WaitUntilAsync(async () =>
                (await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 })).GetProperty("name").GetString() == expected);
            Assert.Equal(expected, (await Read(engine)).GetProperty("Name").GetString());
        }
    }

    [EngineFact]
    public async Task GroupDuplicateAndDeleteEachRecordOneSubtreeAction()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var scenePath = await PrepareAsync(engine, includeChild: true);
        var scene = JsonNode.Parse(await File.ReadAllTextAsync(scenePath))!["Entities"]!.AsArray();
        AddSubtree(4, 2, "Between", includeChild: false);
        AddSubtree(5, 3, "Other root", includeChild: true);
        await File.WriteAllTextAsync(scenePath, scene.Parent!.ToJsonString());
        await engine.StartAsync();

        var original = (await ListEntitiesAsync(engine)).Select(HierarchyRow).ToArray();
        var originalIds = original.Select(row => row.Id).ToHashSet();
        await SelectAsync([5, 2]);
        await Chord(engine, 68);

        (int Id, int Parent, int Order, string? Name)[] duplicated = [];
        await engine.WaitUntilAsync(async () =>
        {
            duplicated = (await ListEntitiesAsync(engine)).Select(HierarchyRow).ToArray();
            return duplicated.Length == original.Length + 4 && duplicated.GroupBy(row => row.Parent)
                .All(siblings => siblings.Select(row => row.Order).SequenceEqual(Enumerable.Range(0, siblings.Count())));
        });
        var copies = duplicated.Where(row => !originalIds.Contains(row.Id)).ToArray();
        var copyRoots = copies.Where(row => row.Parent == 0).ToArray();
        Assert.Equal(2, copyRoots.Length);
        foreach (var root in copyRoots) Assert.Single(copies, row => row.Parent == root.Id);
        await AssertNativeCopiesAsync();

        await Chord(engine, 90);
        await AssertHierarchyAsync(original);
        await AssertCopiesRemovedAsync();
        Assert.False(await CanUndoAsync(engine));

        await Chord(engine, 89);
        await AssertHierarchyAsync(duplicated);
        await AssertNativeCopiesAsync();

        await SelectAsync(copyRoots.Select(row => row.Id).ToArray());
        await engine.CallAsync("rei_editor_send_input", new()
        {
            ["events"] = new[] { new { type = "key_down", code = 261 }, new { type = "key_up", code = 261 } }
        });
        await AssertHierarchyAsync(original);
        await AssertCopiesRemovedAsync();
        await Chord(engine, 90);
        await AssertHierarchyAsync(duplicated);
        await AssertNativeCopiesAsync();
        await Chord(engine, 89);
        await AssertHierarchyAsync(original);
        await AssertCopiesRemovedAsync();

        Task<JsonElement> SelectAsync(int[] ids) => engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = ids });

        Task AssertHierarchyAsync((int Id, int Parent, int Order, string? Name)[] expected) =>
            engine.WaitUntilAsync(async () => (await ListEntitiesAsync(engine)).Select(HierarchyRow).SequenceEqual(expected));

        async Task AssertNativeCopiesAsync()
        {
            foreach (var (id, parent, order, name) in copies)
            {
                var native = await Read(engine, id);
                Assert.Equal((parent, order, name), (native.GetProperty("Parent").GetInt32(),
                    native.GetProperty("Order").GetInt32(), native.GetProperty("Name").GetString()));
            }
        }

        async Task AssertCopiesRemovedAsync()
        {
            foreach (var copy in copies)
                Assert.Equal(JsonValueKind.Null, (await Read(engine, copy.Id)).GetProperty("Name").ValueKind);
        }

        void AddSubtree(int id, int order, string name, bool includeChild)
        {
            var root = scene[1]!.DeepClone();
            root["Id"] = id;
            root["Name"] = name;
            root["Behaviours"]![0]!["SerializedData"]!["_order"]!["Value"] = order;
            scene.Add(root);
            if (!includeChild) return;
            var child = scene[2]!.DeepClone();
            child["Id"] = id + 1;
            child["Behaviours"]![0]!["SerializedData"]!["_parent"]!["Value"] = id;
            scene.Add(child);
        }
    }

    private static (int Id, int Parent, int Order, string? Name) HierarchyRow(JsonElement entity) =>
        (EntityId(entity), entity.GetProperty("parentId").GetInt32(), entity.GetProperty("order").GetInt32(), entity.GetProperty("name").GetString());

    private static async Task<JsonElement[]> ListEntitiesAsync(EngineIntegrationHarness engine) =>
        (await engine.CallAsync("rei_editor_list_entities")).GetProperty("entities").EnumerateArray().ToArray();

    private static async Task<bool> CanUndoAsync(EngineIntegrationHarness engine) =>
        (await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean();

    private static int EntityId(JsonElement entity) => entity.GetProperty("id").GetInt32();

    private async Task<(JsonElement State, JsonElement Frame)> ReadPointerAsync(EngineIntegrationHarness engine, int entityId)
    {
        JsonElement frame = default;
        JsonElement before = default;
        await engine.WaitUntilAsync(async () =>
        {
            var previous = await Read(engine, entityId);
            frame = await engine.CallAsync("rei_editor_capture_frame");
            before = await Read(engine, entityId);
            return before.GetProperty("Hit").ValueKind == JsonValueKind.Array &&
                previous.GetProperty("Width").GetInt32() == before.GetProperty("Width").GetInt32() &&
                previous.GetProperty("Height").GetInt32() == before.GetProperty("Height").GetInt32();
        });
        return (before, frame);
    }

    private static async Task<string> PrepareAsync(EngineIntegrationHarness engine, bool includeChild = false)
    {
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        var scenePath = Path.Combine(engine.ProjectDirectory, "Project", "Scenes", "New Scene.scene");
        var scene = JsonNode.Parse(await File.ReadAllTextAsync(scenePath))!;
        var target = scene["Entities"]!.AsArray().Single(entity => entity!["Id"]!.GetValue<int>() == 2)!;
        var data = target["Behaviours"]![0]!["SerializedData"]!;
        foreach (var field in new[] { "_position", "_rotation" })
            foreach (var axis in new[] { "x", "y", "z" }) data[field]!["Value"]![axis]!["Value"] = 0;
        if (includeChild)
        {
            var entities = scene["Entities"]!.AsArray();
            var child = target.DeepClone();
            child["Id"] = entities.Max(entity => entity!["Id"]!.GetValue<int>()) + 1;
            child["Name"] = "Source Child";
            child["Behaviours"]![0]!["SerializedData"]!["_parent"]!["Value"] = 2;
            child["Behaviours"]![0]!["SerializedData"]!["_order"]!["Value"] = 0;
            child["Behaviours"]![0]!["SerializedData"]!["_position"]!["Value"]!["x"]!["Value"] = 1.25;
            entities.Add(child);
        }
        await File.WriteAllTextAsync(scenePath, scene.ToJsonString());
        var source = PROBE_SOURCE.Replace("__STATE__", Path.Combine(engine.ProjectDirectory, "input-state.json"))
            .Replace("__COMMAND__", Path.Combine(engine.ProjectDirectory, "input-step.txt"));
        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project", "Scripts", "ReiApp.cpp"), source);
        return scenePath;
    }

    private static double[] Position(JsonElement state) => state.GetProperty("Position").EnumerateArray().Select(value => value.GetDouble()).ToArray();
    private static Task<JsonElement> Drag(EngineIntegrationHarness engine, double x, double y, double dx) => engine.CallAsync("rei_editor_drag_pointer", new() { ["fromX"] = x, ["fromY"] = y, ["toX"] = x + dx, ["toY"] = y });
    private static Task<JsonElement> Chord(EngineIntegrationHarness engine, int key) => engine.CallAsync("rei_editor_send_input", new()
    {
        ["events"] = new[] { new { type = "key_down", code = 341 }, new { type = "key_down", code = key }, new { type = "key_up", code = key }, new { type = "key_up", code = 341 } }
    });

    private async Task WaitPosition(EngineIntegrationHarness engine, double[] expected, int entityId = 2)
    {
        await engine.WaitUntilAsync(async () => Position(await Read(engine, entityId)).Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 1e-4));
        await AssertEditorPosition(engine, expected, entityId);
    }

    private static async Task AssertEditorPosition(EngineIntegrationHarness engine, double[] expected, int entityId = 2)
    {
        await engine.WaitUntilAsync(async () =>
        {
            var entity = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = entityId });
            var transform = entity.GetProperty("behaviours").EnumerateArray().Single(behaviour => behaviour.GetProperty("name").GetString() == "Transform");
            var position = transform.GetProperty("properties").EnumerateArray().Single(property => property.GetProperty("name").GetString() == "_position").GetProperty("value");
            return new[] { "x", "y", "z" }.Select(axis => position.GetProperty(axis).GetDouble()).Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 1e-4);
        });
    }

    private async Task<JsonElement> Read(EngineIntegrationHarness engine, int entityId = 2)
    {
        var step = ++_step;
        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "input-step.txt"), $"{step} {entityId}");
        JsonElement state = default;
        await engine.WaitUntilAsync(async () =>
        {
            try
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory, "input-state.json")));
                if (document.RootElement.GetProperty("Step").GetInt32() == step) state = document.RootElement.Clone();
            }
            catch (IOException) { }
            catch (JsonException) { }
            return state.ValueKind != JsonValueKind.Undefined;
        });
        return state;
    }

    private const string PROBE_SOURCE = """
#include "Startup/AppEntryPoint.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include <fstream>

class TestApplication final : public rei::App
{
    i32 _step = 0;
    f64 _scrollTotal = 0;
public:
    void OnStart() override
    {
        rei::GetInternalWorld()->AddSystem([this]
        {
            _scrollTotal += rei::Input::GetScrollY();
            i32 step = 0;
            std::ifstream command(R"(__COMMAND__)");
            i32 entityId = 0;
            if (!(command >> step >> entityId) || step <= _step) return;
            _step = step;
            ECS_WORLD(rei::GetInternalWorld());
            const auto target = rei::GetEntityManager().GetBySceneId(entityId);
            nlohmann::json position = nullptr;
            nlohmann::json name = nullptr;
            i32 parentId = 0;
            i32 order = 0;
            if (!IS_DEAD(target))
            {
                const auto& transform = GET(target, rei::Transform);
                const auto p = transform.GetLocalPosition();
                position = {p.x, p.y, p.z};
                order = transform.GetChildOrder();
                name = GET(target, EntityInfo).Name;
                const auto parent = transform.GetParent();
                if (!IS_DEAD(parent)) parentId = GET(parent, EntityInfo).Id;
            }
            nlohmann::json hit = nullptr;
            const auto camera = rei::render::Camera::GetMainCamera();
            const auto controlEntity = FILTER(rei::editor::TransformationControl)->First();
            if (!camera.IsNull() && !IS_DEAD(controlEntity))
            {
                const auto& control = GET(controlEntity, rei::editor::TransformationControl);
                const auto arrow = control.RightMovementArrow.Entity;
                if (control.HasTargets() && !IS_DEAD(arrow))
                {
                    const auto& collider = GET(arrow, rei::physics::PointerCollisionListener).Collider;
                    const auto matrix = GET(arrow, rei::Transform).CalculateWorldModelMatrix();
                    
                    for (i32 offset = 35; offset < 150 && hit.is_null(); offset++)
                    for (i32 dy = -20; dy <= 20 && hit.is_null(); dy++)
                    {
                        const f32 x = control.PivotScreenPosition.x + offset;
                        const f32 y = control.PivotScreenPosition.y + dy;
                        rei::math::Vector3 point;
                        if (collider && collider->Intersect(camera.Get().GetScreenPointToRay(x, y), matrix, point))
                            hit = {x, y};
                    }
                }
            }
            const nlohmann::json state = {{"Mouse", {rei::Input::GetMouseX(), rei::Input::GetMouseY()}}, {"ScrollTotal", _scrollTotal},
                {"Width", rei::Input::GetMouseState().Width}, {"Height", rei::Input::GetMouseState().Height}, {"Step", step}, {"Position", position}, {"Name", name}, {"Parent", parentId}, {"Order", order}, {"Hit", hit},
                {"KeyDown", rei::Input::IsKeyDown(GLFW_KEY_W)}, {"ButtonDown", rei::Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT)}};
            std::ofstream output(R"(__STATE__)");
            output << state.dump();
        });
    }
};
std::shared_ptr<rei::App> CreateApp() { return std::make_shared<TestApplication>(); }
""";
}
