using System.Text.Json;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "ViewportGizmoChecklist")]
public sealed class ViewportGizmoChecklistTests(ITestOutputHelper output)
{
    private sealed record Scenario(string Part, int Key, int Mode, int Count, bool Rect = false, bool Stretch = false)
    {
        public override string ToString() => $"{(Rect ? Stretch ? "stretch" : "fixed" : "3D")}/{Part}/{Count}";
    }

    private int _step;

    [EngineFact]
    public async Task GizmoChecklistThroughMcpInputAndSharedHistory()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        var source = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ViewportGizmoProbe.cpp")))
            .Replace("__STATE__", Path.Combine(engine.ProjectDirectory, "gizmo-state.json"))
            .Replace("__COMMAND__", Path.Combine(engine.ProjectDirectory, "gizmo-command.json"));
        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "Project", "Scripts", "ReiApp.cpp"), source);
        await engine.StartAsync();
        var scenePath = Path.Combine(engine.ProjectDirectory, "Project", "Scenes", "New Scene.scene");
        var disk = await File.ReadAllBytesAsync(scenePath);
        var failures = new List<string>();
        var results = new List<object>();
        var filter = Environment.GetEnvironmentVariable("REI_GIZMO_CHECKLIST_FILTER");
        var scenarios = Scenarios().Where(s => filter == null || s.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.NotEmpty(scenarios);
        foreach (var scenario in scenarios)
        {
            output.WriteLine($"Checking {scenario}");
            try
            {
                await Check(engine, scenario);
                results.Add(new { scenario = scenario.ToString(), passed = true });
                output.WriteLine($"PASS {scenario}");
            }
            catch (Exception error)
            {
                var message = $"{scenario}: {error.Message}";
                failures.Add(message);
                results.Add(new { scenario = scenario.ToString(), passed = false, error = error.ToString() });
                output.WriteLine($"FAIL {message}");
                if ((await engine.CallAsync("rei_editor_get_state")).GetProperty("engine").GetProperty("status").GetString() != "running") break;
                // Finish traversing this case before disposing its targets; do not hide failures.
                for (var i = 0; i < 4 && (await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean(); i++)
                    await engine.CallAsync("rei_editor_undo");
            }
            await File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, "gizmo-checklist.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.Equal(disk, await File.ReadAllBytesAsync(scenePath));
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static IEnumerable<Scenario> Scenarios()
    {
        foreach (var count in new[] { 1, 2 })
        {
            foreach (var part in new[] { "move_x", "move_y", "move_z", "move_xy", "move_xz", "move_yz" }) yield return new(part, 87, 0, count);
            foreach (var part in new[] { "scale_x", "scale_y", "scale_z", "scale_all" }) yield return new(part, 69, 1, count);
            foreach (var part in new[] { "rotate_x", "rotate_y", "rotate_z" }) yield return new(part, 82, 2, count);
            foreach (var stretch in new[] { false, true })
            {
                foreach (var part in new[] { "move_x", "move_y", "move_xy" }) yield return new(part, 87, 0, count, true, stretch);
                foreach (var part in new[] { "body", "rect_l", "rect_r", "rect_t", "rect_b", "rect_tl", "rect_tr", "rect_bl", "rect_br" }) yield return new(part, 84, 3, count, true, stretch);
            }
        }
    }

    private async Task Check(EngineIntegrationHarness engine, Scenario scenario)
    {
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        var configured = await Command(engine, "configure", new() { ["Count"] = scenario.Count, ["Rect"] = scenario.Rect, ["Stretch"] = scenario.Stretch, ["Part"] = scenario.Part });
        var ids = configured.GetProperty("Entities").EnumerateArray().Select(entity => entity.GetProperty("Id").GetInt32()).ToArray();
        await engine.WaitUntilAsync(async () =>
        {
            var listed = (await engine.CallAsync("rei_editor_list_entities")).GetProperty("entities").EnumerateArray().Select(entity => entity.GetProperty("id").GetInt32()).ToHashSet();
            return ids.All(listed.Contains);
        });
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = ids });
        await AssertEditor(engine, configured);
        var (before, frame) = await Pointer(engine, scenario);
        var end = EndPoint(before, scenario);
        await Drag(engine, before, frame, end.X, end.Y);
        var after = await Command(engine);
        Assert.Equal(scenario.Part, after.GetProperty("ActivePart").GetString());
        AssertGesture(scenario, before, after);
        await AssertEditor(engine, after);
        await engine.CallAsync("rei_editor_capture_frame");
        await File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, $"{scenario.ToString().Replace('/', '-')}.json"), JsonSerializer.Serialize(new { before, after }));

        // Regression: stretch-corner Undo once gained an extra history entry without input.
        var replayCount = scenario is { Rect: true, Stretch: true, Part: "rect_br", Count: 1 } ? 20 : 2;
        for (var replay = 0; replay < replayCount; replay++)
        {
            await engine.CallAsync("rei_editor_undo");
            await AssertState(engine, before);
            Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
            await engine.CallAsync("rei_editor_redo");
            await AssertState(engine, after);
            Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());
        }
        await engine.CallAsync("rei_editor_undo");
        await AssertState(engine, before);
        await Command(engine, "arm");
        (before, frame) = await Pointer(engine, scenario);
        var hit = before.GetProperty("Hit");
        await Drag(engine, before, frame, hit[0].GetDouble(), hit[1].GetDouble());
        var empty = await Command(engine);
        Assert.Equal(scenario.Part, empty.GetProperty("ActivePart").GetString());
        Assert.True(EqualFields(before, empty), $"Empty drag changed state: {empty}");
        var history = await engine.CallAsync("rei_editor_get_command_state");
        Assert.False(history.GetProperty("canUndo").GetBoolean());
        Assert.True(history.GetProperty("canRedo").GetBoolean());
    }

    private static (double X, double Y) EndPoint(JsonElement before, Scenario scenario)
    {
        var hit = before.GetProperty("Hit");
        var x = hit[0].GetDouble();
        var y = hit[1].GetDouble();
        if (scenario.Mode == 2)
        {
            var pivot = before.GetProperty("Pivot");
            var dx = x - pivot[0].GetDouble();
            var dy = y - pivot[1].GetDouble();
            var length = Math.Sqrt(dx * dx + dy * dy);
            return (x - dy / length * 16, y + dx / length * 16);
        }
        if (scenario.Part.StartsWith("rect_"))
        {
            var side = scenario.Part[5..];
            return (x + (side.Contains('l') ? -24 : side.Contains('r') ? 24 : 0), y + (side.Contains('t') ? -20 : side.Contains('b') ? 20 : 0));
        }
        if (scenario.Part == "body") return (x + 24, y + 20);
        var pivotPoint = before.GetProperty("Pivot");
        var vx = x - pivotPoint[0].GetDouble();
        var vy = y - pivotPoint[1].GetDouble();
        var distance = Math.Sqrt(vx * vx + vy * vy);
        return scenario.Part == "scale_all" ? (x + 24, y - 24) : (x + vx / distance * 24, y + vy / distance * 24);
    }

    private static void AssertGesture(Scenario scenario, JsonElement before, JsonElement after)
    {
        var field = scenario.Rect ? scenario.Mode == 3 && scenario.Part != "body" ? "_sizeDelta" : "_anchoredPosition"
            : scenario.Mode == 0 ? "_position" : scenario.Mode == 1 ? "_scale" : "_rotation";
        foreach (var (a, b) in before.GetProperty("Entities").EnumerateArray().Zip(after.GetProperty("Entities").EnumerateArray()))
        {
            Assert.False(EqualVector(a.GetProperty("Fields").GetProperty(field), b.GetProperty("Fields").GetProperty(field), field), $"No {field} change for {scenario}: {after}");
            foreach (var property in a.GetProperty("Fields").EnumerateObject())
            {
                if (property.Name == field || (field == "_sizeDelta" && property.Name == "_anchoredPosition")) continue;
                Assert.True(EqualVector(property.Value, b.GetProperty("Fields").GetProperty(property.Name), property.Name), $"Unexpected {property.Name} change: {scenario}");
            }
        }
        if (!scenario.Rect || !scenario.Part.StartsWith("rect_")) return;
        var oldBounds = UnionBounds(before);
        var newBounds = UnionBounds(after);
        var side = scenario.Part[5..];
        foreach (var index in new[] { 0, 1, 2, 3 })
        {
            var moves = index == 0 ? side.Contains('l') : index == 2 ? side.Contains('r') : index == 1 ? side.Contains('b') : side.Contains('t');
            if (!moves) Assert.InRange(Math.Abs(oldBounds[index] - newBounds[index]), 0, 0.002);
        }
    }

    private static double[] UnionBounds(JsonElement state)
    {
        var bounds = state.GetProperty("Entities").EnumerateArray().Select(e => e.GetProperty("Bounds").EnumerateArray().Select(n => n.GetDouble()).ToArray()).ToArray();
        return [bounds.Min(b => b[0]), bounds.Min(b => b[1]), bounds.Max(b => b[2]), bounds.Max(b => b[3])];
    }

    private static bool EqualVector(JsonElement a, JsonElement b, string name) => a.GetArrayLength() == b.GetArrayLength() && a.EnumerateArray().Zip(b.EnumerateArray()).All(pair =>
    {
        var delta = pair.First.GetDouble() - pair.Second.GetDouble();
        if (name == "_rotation") delta = Math.IEEERemainder(delta, 360);
        return Math.Abs(delta) < 0.001;
    });

    private static bool EqualFields(JsonElement a, JsonElement b) => a.GetProperty("Entities").GetArrayLength() == b.GetProperty("Entities").GetArrayLength() && a.GetProperty("Entities").EnumerateArray().Zip(b.GetProperty("Entities").EnumerateArray()).All(pair =>
        pair.First.GetProperty("Id").GetInt32() == pair.Second.GetProperty("Id").GetInt32() && pair.First.GetProperty("Fields").EnumerateObject().All(property => EqualVector(property.Value, pair.Second.GetProperty("Fields").GetProperty(property.Name), property.Name)));

    private async Task AssertState(EngineIntegrationHarness engine, JsonElement expected)
    {
        await engine.WaitUntilAsync(async () => EqualFields(expected, await Command(engine)));
        await AssertEditor(engine, expected);
    }

    private static async Task AssertEditor(EngineIntegrationHarness engine, JsonElement native)
    {
        var entities = native.GetProperty("Entities").EnumerateArray().ToArray();
        var ids = entities.Select(entity => entity.GetProperty("Id").GetInt32()).ToArray();
        try
        {
            // Inspector hydrates/polls the displayed entity, not every group member.
            foreach (var entity in entities)
            {
                var id = entity.GetProperty("Id").GetInt32();
                await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { id } });
                await engine.WaitUntilAsync(async () =>
                {
                    var editor = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = id });
                    var properties = editor.GetProperty("behaviours").EnumerateArray().SelectMany(b => b.GetProperty("properties").EnumerateArray()).ToDictionary(p => p.GetProperty("name").GetString()!);
                    foreach (var field in entity.GetProperty("Fields").EnumerateObject())
                    {
                        if (!properties.TryGetValue(field.Name, out var property)) return false;
                        var vector = property.GetProperty("value");
                        var values = new[] { "x", "y", "z" }.Take(field.Value.GetArrayLength()).Select(axis => vector.GetProperty(axis).GetDouble()).ToArray();
                        if (!EqualVector(field.Value, JsonSerializer.SerializeToElement(values), field.Name)) return false;
                    }
                    return true;
                });
            }
        }
        finally { await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = ids }); }
    }

    private static Task<JsonElement> Drag(EngineIntegrationHarness engine, JsonElement state, JsonElement frame, double toX, double toY)
    {
        var hit = state.GetProperty("Hit");
        var sx = frame.GetProperty("width").GetDouble() / state.GetProperty("Width").GetDouble();
        var sy = frame.GetProperty("height").GetDouble() / state.GetProperty("Height").GetDouble();
        return engine.CallAsync("rei_editor_drag_pointer", new() { ["fromX"] = hit[0].GetDouble() * sx, ["fromY"] = hit[1].GetDouble() * sy, ["toX"] = toX * sx, ["toY"] = toY * sy });
    }

    private async Task<(JsonElement State, JsonElement Frame)> Pointer(EngineIntegrationHarness engine, Scenario scenario)
    {
        var current = await Command(engine);
        var ids = current.GetProperty("Entities").EnumerateArray().Select(e => e.GetProperty("Id").GetInt32()).ToHashSet();
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = ids.ToArray() });
        await engine.WaitUntilAsync(async () =>
        {
            var selection = await engine.CallAsync("rei_editor_get_selection_state", new() { ["source"] = "runtime" });
            var selected = selection.GetProperty("selectedIds").EnumerateArray().Select(id => id.GetInt32()).ToHashSet();
            if (!ids.SetEquals(selected)) return false;
            var control = await Command(engine);
            return ids.SetEquals(control.GetProperty("Selected").EnumerateArray().Select(id => id.GetInt32())) && control.GetProperty("RectTargets").GetBoolean() == scenario.Rect;
        });
        await engine.CallAsync("rei_editor_send_input", new() { ["events"] = new object[] { new { type = "wait" }, new { type = "key_down", code = scenario.Key }, new { type = "key_up", code = scenario.Key } } });
        JsonElement state = default, frame = default;
        await engine.WaitUntilAsync(async () =>
        {
            var first = await Command(engine, pointer: true);
            frame = await engine.CallAsync("rei_editor_capture_frame");
            state = await Command(engine, pointer: true);
            return state.GetProperty("Mode").GetInt32() == scenario.Mode && state.GetProperty("Hit").ValueKind == JsonValueKind.Array &&
                first.GetProperty("Width").GetInt32() == state.GetProperty("Width").GetInt32() && first.GetProperty("Height").GetInt32() == state.GetProperty("Height").GetInt32();
        });
        return (state, frame);
    }

    private async Task<JsonElement> Command(EngineIntegrationHarness engine, string action = "read", Dictionary<string, object?>? fields = null, bool pointer = false)
    {
        var command = fields ?? new();
        command["Step"] = ++_step;
        command["Action"] = action;
        command["Pointer"] = pointer;
        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "gizmo-command.json"), JsonSerializer.Serialize(command));
        JsonElement state = default;
        await engine.WaitUntilAsync(async () =>
        {
            try
            {
                // Permit the native probe to publish while polling; denying writes can lose its one-shot response.
                await using var file = new FileStream(Path.Combine(engine.ProjectDirectory, "gizmo-state.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var document = await JsonDocument.ParseAsync(file);
                if (document.RootElement.GetProperty("Step").GetInt32() == _step) state = document.RootElement.Clone();
            }
            catch (IOException) { }
            catch (JsonException) { }
            return state.ValueKind != JsonValueKind.Undefined;
        });
        return state;
    }
}
