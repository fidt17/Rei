using System.Text.Json;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Lifecycle")]
[Trait("Area", "EditorCommands")]
public sealed class NativeHistoryLifecycleTests(ITestOutputHelper output)
{
    private int _step;

    [EngineFact]
    public Task OldNativeHistoryCannotReplayAfterPlayStop() => VerifyRestartAsync(reloadDll: false);

    [EngineFact]
    public Task OldNativeHistoryCannotReplayAfterDllReload() => VerifyRestartAsync(reloadDll: true);

    private async Task VerifyRestartAsync(bool reloadDll)
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var appPath = await StartAsync(engine);
        var scenePath = Path.Combine(engine.ProjectDirectory, "Project", "Scenes", "New Scene.scene");
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 2 } });
        var baseline = Position(await Command(engine));
        var oldMove = await Command(engine, "move", baseline[0] + 10);
        await AssertPosition(engine, Position(oldMove));
        await engine.WaitUntilAsync(async () => (await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await RestartAsync(engine, appPath, reloadDll);
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 2 } });
        // Play/import may save the edited scene before restarting the engine.
        var restarted = Position(await Command(engine));
        await AssertPosition(engine, restarted);
        var disk = await File.ReadAllBytesAsync(scenePath);
        var newMove = await Command(engine, "move", restarted[0] + 20);
        await AssertPosition(engine, Position(newMove));
        output.WriteLine($"Old native action: {oldMove.GetProperty("ActionId")}; new native action: {newMove.GetProperty("ActionId")}");
        Assert.NotEqual(0ul, oldMove.GetProperty("ActionId").GetUInt64());
        Assert.NotEqual(0ul, newMove.GetProperty("ActionId").GetUInt64());
        await engine.WaitUntilAsync(async () => (await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.CallAsync("rei_editor_undo");
        await AssertPosition(engine, restarted);
        await engine.CallAsync("rei_editor_redo");
        await AssertPosition(engine, Position(newMove));
        await engine.CallAsync("rei_editor_undo");
        await AssertPosition(engine, restarted);
        var history = await engine.CallAsync("rei_editor_get_command_state");
        Assert.True(history.GetProperty("canRedo").GetBoolean());
        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.CallAsync("rei_editor_undo"));
        Assert.Matches("undo_(unavailable|failed)", rejected.Message);
        await AssertPosition(engine, restarted);
        await engine.CallAsync("rei_editor_redo");
        await AssertPosition(engine, Position(newMove));
        Assert.Equal(disk, await File.ReadAllBytesAsync(scenePath));
    }

    [EngineFact]
    public async Task UndoReachesRenameAfterNativeMoveAndPlayStop()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var appPath = await StartAsync(engine);
        var before = await Command(engine);
        var originalName = before.GetProperty("Name").GetString();
        const string RENAMED = "Rename before native move";
        await engine.CallAsync("rei_editor_rename_entity", new() { ["entityId"] = 2, ["newName"] = RENAMED });

        var baseline = Position(before);
        var move = await Command(engine, "move", baseline[0] + 10);
        Assert.NotEqual(0ul, move.GetProperty("ActionId").GetUInt64());
        await AssertPosition(engine, Position(move));
        await engine.CallAsync("rei_editor_undo");
        await AssertPosition(engine, baseline);
        await engine.CallAsync("rei_editor_redo");
        await AssertPosition(engine, Position(move));

        await RestartAsync(engine, appPath, reloadDll: false);
        Assert.Equal(RENAMED, (await Command(engine)).GetProperty("Name").GetString());
        Assert.True((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.CallAsync("rei_editor_undo");
        var restored = await Command(engine);
        Assert.Equal(originalName, restored.GetProperty("Name").GetString());
        var editor = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 });
        Assert.Equal(originalName, editor.GetProperty("name").GetString());
        await AssertPosition(engine, Position(move));
    }

    private static async Task<string> StartAsync(EngineIntegrationHarness engine)
    {
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        var appPath = Path.Combine(engine.ProjectDirectory, "Project", "Scripts", "ReiApp.cpp");
        var source = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NativeHistoryProbe.cpp")))
            .Replace("__STATE__", Path.Combine(engine.ProjectDirectory, "history-state.json"))
            .Replace("__COMMAND__", Path.Combine(engine.ProjectDirectory, "history-command.json"));
        await File.WriteAllTextAsync(appPath, source);
        await engine.StartAsync();
        return appPath;
    }

    private static async Task RestartAsync(EngineIntegrationHarness engine, string appPath, bool reloadDll)
    {
        var oldSession = await Session(engine);
        Assert.NotNull(oldSession);
        // A restarted probe must not execute the previous move command again.
        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "history-command.json"), "{\"Step\":0}");
        if (reloadDll)
        {
            await File.AppendAllTextAsync(appPath, "\n// Force project DLL reload through the normal import pipeline.\n");
            await engine.RunOperationAsync("rei_editor_refresh_assets");
        }
        else
        {
            await engine.RunOperationAsync("rei_editor_start_playmode");
            await engine.RunOperationAsync("rei_editor_stop_playmode");
        }
        await engine.WaitUntilAsync(async () =>
        {
            var state = await engine.CallAsync("rei_editor_get_state");
            var session = await Session(engine);
            return state.GetProperty("engine").GetProperty("status").GetString() == "running" &&
                state.GetProperty("engine").GetProperty("mode").GetString() == "EditorMode" &&
                !state.GetProperty("automation").GetProperty("isBuilding").GetBoolean() && session != null && session != oldSession;
        }, TimeSpan.FromMinutes(2));
    }

    private static async Task<string?> Session(EngineIntegrationHarness engine)
    {
        var profile = await engine.CallAsync("rei_editor_get_profiling_snapshot", new() { ["source"] = "runtime", ["view"] = "recent", ["limit"] = 1 });
        return profile.TryGetProperty("sessionId", out var session) ? session.GetString() : null;
    }

    private static double[] Position(JsonElement state) => state.GetProperty("Position").EnumerateArray().Select(value => value.GetDouble()).ToArray();

    private async Task AssertPosition(EngineIntegrationHarness engine, double[] expected)
    {
        await engine.WaitUntilAsync(async () => Position(await Command(engine)).Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 0.0001));
        // Bind Inspector to the current scene entity after DLL reload, rather than its old selection object.
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = Array.Empty<int>() });
        await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { 2 } });
        await engine.WaitUntilAsync(async () =>
        {
            var entity = await engine.CallAsync("rei_editor_get_entity", new() { ["entityId"] = 2 });
            var transform = entity.GetProperty("behaviours").EnumerateArray().Single(behaviour => behaviour.GetProperty("name").GetString() == "Transform");
            var position = transform.GetProperty("properties").EnumerateArray().Single(property => property.GetProperty("name").GetString() == "_position").GetProperty("value");
            return new[] { "x", "y", "z" }.Select(axis => position.GetProperty(axis).GetDouble()).Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 0.0001);
        });
    }

    private async Task<JsonElement> Command(EngineIntegrationHarness engine, string action = "read", double x = 0)
    {
        var step = ++_step;
        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "history-command.json"), JsonSerializer.Serialize(new { Step = step, Action = action, X = x }));
        JsonElement state = default;
        await engine.WaitUntilAsync(async () =>
        {
            try
            {
                await using var file = new FileStream(Path.Combine(engine.ProjectDirectory, "history-state.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var document = await JsonDocument.ParseAsync(file);
                if (document.RootElement.GetProperty("Step").GetInt32() == step) state = document.RootElement.Clone();
            }
            catch (IOException) { }
            catch (JsonException) { }
            return state.ValueKind != JsonValueKind.Undefined;
        });
        return state;
    }
}
