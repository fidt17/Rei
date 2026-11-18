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
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        var scenePath = Path.Combine(engine.ProjectDirectory, "Project", "Scenes", "New Scene.scene");
        var scene = JsonNode.Parse(await File.ReadAllTextAsync(scenePath))!;
        var target = scene["Entities"]!.AsArray().Single(entity => entity!["Id"]!.GetValue<int>() == 2)!;
        var data = target["Behaviours"]![0]!["SerializedData"]!;
        foreach (var field in new[] { "_position", "_rotation" })
            foreach (var axis in new[] { "x", "y", "z" }) data[field]!["Value"]![axis]!["Value"] = 0;
        await File.WriteAllTextAsync(scenePath, scene.ToJsonString());
        var source = PROBE_SOURCE.Replace("__STATE__", Path.Combine(engine.ProjectDirectory, "input-state.json"))
            .Replace("__COMMAND__", Path.Combine(engine.ProjectDirectory, "input-step.txt"));
        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project", "Scripts", "ReiApp.cpp"), source);
        await engine.StartAsync();
        var disk = await File.ReadAllBytesAsync(scenePath);
        var entity = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 });
        var originalName = entity.GetProperty("name").GetString();
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 2 } });
        await engine.CallAsync("rei_editor_rename_entity", new() { ["entityId"] = 2, ["newName"] = "Input rename" });
        JsonElement frame = default;
        JsonElement before = default;
        await engine.WaitUntilAsync(async () =>
        {
            var previous = await Read(engine);
            frame = await engine.CallAsync("rei_editor_capture_frame");
            before = await Read(engine);
            return before.GetProperty("Hit").ValueKind == JsonValueKind.Array &&
                previous.GetProperty("Width").GetInt32() == before.GetProperty("Width").GetInt32() &&
                previous.GetProperty("Height").GetInt32() == before.GetProperty("Height").GetInt32();
        });
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

    private static double[] Position(JsonElement state) => state.GetProperty("Position").EnumerateArray().Select(value => value.GetDouble()).ToArray();
    private static Task<JsonElement> Drag(EngineIntegrationHarness engine, double x, double y, double dx) => engine.CallAsync("rei_editor_drag_pointer", new() { ["fromX"] = x, ["fromY"] = y, ["toX"] = x + dx, ["toY"] = y });
    private static Task<JsonElement> Chord(EngineIntegrationHarness engine, int key) => engine.CallAsync("rei_editor_send_input", new()
    {
        ["events"] = new[] { new { type = "key_down", code = 341 }, new { type = "key_down", code = key }, new { type = "key_up", code = key }, new { type = "key_up", code = 341 } }
    });

    private async Task WaitPosition(EngineIntegrationHarness engine, double[] expected)
    {
        await engine.WaitUntilAsync(async () => Position(await Read(engine)).Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 1e-4));
        await AssertEditorPosition(engine, expected);
    }

    private static async Task AssertEditorPosition(EngineIntegrationHarness engine, double[] expected)
    {
        await engine.WaitUntilAsync(async () =>
        {
            var entity = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 });
            var transform = entity.GetProperty("behaviours").EnumerateArray().Single(behaviour => behaviour.GetProperty("name").GetString() == "Transform");
            var position = transform.GetProperty("properties").EnumerateArray().Single(property => property.GetProperty("name").GetString() == "_position").GetProperty("value");
            return new[] { "x", "y", "z" }.Select(axis => position.GetProperty(axis).GetDouble()).Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 1e-4);
        });
    }

    private async Task<JsonElement> Read(EngineIntegrationHarness engine)
    {
        var step = ++_step;
        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "input-step.txt"), step.ToString());
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
            if (!(command >> step) || step <= _step) return;
            _step = step;
            ECS_WORLD(rei::GetInternalWorld());
            const auto target = rei::GetEntityManager().GetBySceneId(2);
            const auto position = GET(target, rei::Transform).GetLocalPosition();
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
                {"Width", rei::Input::GetMouseState().Width}, {"Height", rei::Input::GetMouseState().Height}, {"Step", step}, {"Position", {position.x, position.y, position.z}}, {"Hit", hit},
                {"KeyDown", rei::Input::IsKeyDown(GLFW_KEY_W)}, {"ButtonDown", rei::Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT)}};
            std::ofstream output(R"(__STATE__)");
            output << state.dump();
        });
    }
};
std::shared_ptr<rei::App> CreateApp() { return std::make_shared<TestApplication>(); }
""";
}
