using System.Text.Json;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Lifecycle")]
[Trait("Area", "EditorCommands")]
public sealed class EditorCommandLifecycleTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task RenameUndoRedoReachNativeStateWithoutSavingAndRespectPlayStop()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        await SceneSynchronizationTests.PrepareProbeAsync(engine.ProjectDirectory, 0);
        await engine.StartAsync();
        var probe = new SceneSynchronizationTests(output);
        var before = Name(await probe.CommandAsync(engine, "read"));
        var scenePath = Path.Combine(engine.ProjectDirectory, "Project", "Scenes", "New Scene.scene");
        var diskBefore = await File.ReadAllBytesAsync(scenePath);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.AssertToolErrorAsync("rei_editor_undo", new(), "undo_unavailable");

        await engine.CallAsync("rei_editor_rename_entity", new() { ["entityId"] = 2, ["newName"] = "Command Rename" });
        Assert.Equal("Command Rename", Name(await probe.CommandAsync(engine, "read")));
        Assert.True((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.CallAsync("rei_editor_undo");
        Assert.Equal(before, Name(await probe.CommandAsync(engine, "read")));
        await engine.CallAsync("rei_editor_redo");
        Assert.Equal("Command Rename", Name(await probe.CommandAsync(engine, "read")));
        Assert.Equal(diskBefore, await File.ReadAllBytesAsync(scenePath));
        await engine.CallAsync("rei_editor_save_project");
        await AssertSavedName(scenePath, "Command Rename");
        await engine.CallAsync("rei_editor_undo");
        Assert.Equal(before, Name(await probe.CommandAsync(engine, "read")));
        await AssertSavedName(scenePath, "Command Rename");
        await engine.CallAsync("rei_editor_redo");

        await File.WriteAllTextAsync(Path.Combine(engine.ProjectDirectory, "scene-sync-command.json"), "{\"Step\":0}");
        await engine.RunOperationAsync("rei_editor_start_playmode");
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.AssertToolErrorAsync("rei_editor_undo", new(), "undo_unavailable");
        await engine.AssertToolErrorAsync("rei_editor_rename_entity", new() { ["entityId"] = 2, ["newName"] = "Blocked in Play" }, "rename_failed");
        Assert.Equal("Command Rename", Name(await probe.CommandAsync(engine, "read")));
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await engine.WaitUntilAsync(async () => (await engine.CallAsync("rei_editor_get_state")).GetProperty("engine").GetProperty("status").GetString() == "running");
        Assert.Equal("Command Rename", Name(await probe.CommandAsync(engine, "read")));
        await engine.CallAsync("rei_editor_rename_entity", new() { ["entityId"] = 2, ["newName"] = "After Stop" });
        Assert.Equal("After Stop", Name(await probe.CommandAsync(engine, "read")));
        await engine.CallAsync("rei_editor_undo");
        Assert.Equal("Command Rename", Name(await probe.CommandAsync(engine, "read")));
        await AssertSavedName(scenePath, "Command Rename");
    }

    private static string Name(JsonElement packet) => packet.GetProperty("Entities").EnumerateArray().Single(entity => entity.GetProperty("Id").GetInt32() == 2).GetProperty("Name").GetString()!;
    private static async Task AssertSavedName(string path, string expected)
    {
        using var scene = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(expected, scene.RootElement.GetProperty("Entities").EnumerateArray().Single(entity => entity.GetProperty("Id").GetInt32() == 2).GetProperty("Name").GetString());
    }
}
