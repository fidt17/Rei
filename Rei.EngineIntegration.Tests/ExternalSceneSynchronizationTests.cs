using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "SceneSync")]
public sealed class ExternalSceneSynchronizationTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task SelectionAndReparentMatchNativeStateInIsolatedProject()
    {
        var source = Environment.GetEnvironmentVariable("REI_SCENE_SYNC_PROJECT");
        if (source == null) return;
        var preparedRun = Environment.GetEnvironmentVariable("REI_SCENE_SYNC_PREPARED_RUN");
        var before = await ExternalProjectCopy.FingerprintAsync(source);
        await using var engine = new EngineIntegrationHarness(sourceProjectDirectory: source, preparedRunDirectory: preparedRun);
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        try
        {
            await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
            if (preparedRun != null) RestorePreparedProject(source, engine.ProjectDirectory);
            await ExternalProjectCopy.LocalizeBuildFilesAsync(source, engine.ProjectDirectory);
            await SceneSynchronizationTests.PrepareProbeAsync(engine.ProjectDirectory, 0, preserveApplication: true);
            File.Delete(Path.Combine(engine.ProjectDirectory, "scene-sync-command.json"));
            File.Delete(Path.Combine(engine.ProjectDirectory, "scene-sync-state.json"));
            await engine.StartAsync();
            var probe = new SceneSynchronizationTests(output);
            var initial = await probe.CommandAsync(engine, "read");
            var entities = initial.GetProperty("Entities").EnumerateArray().ToArray();
            Assert.True(entities.Length >= 2);
            var parentIds = entities.Select(entity => entity.GetProperty("ParentId").GetInt32()).ToHashSet();
            var moved = entities.Last(entity => !parentIds.Contains(entity.GetProperty("Id").GetInt32())).GetProperty("Id").GetInt32();
            var parent = entities.First(entity => entity.GetProperty("Id").GetInt32() != moved).GetProperty("Id").GetInt32();
            await probe.CommandAsync(engine, "select", ("Ids", new[] { parent }));
            await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { moved } });
            await probe.ReadUntilAsync(engine, packet => packet.GetProperty("SelectedIds").EnumerateArray().Select(id => id.GetInt32()).SequenceEqual(new[] { moved }));
            await engine.AssertToolErrorAsync("rei_editor_set_entity_parent", new() { ["entityId"] = moved, ["parentId"] = moved, ["order"] = 0 }, "invalid_parent");
            await engine.AssertToolErrorAsync("rei_editor_set_entity_parent", new() { ["entityId"] = moved, ["parentId"] = 0, ["order"] = -1 }, "invalid_order");
            foreach (var target in new[] { parent, 0, parent, 0 })
            {
                var accepted = await engine.CallAsync("rei_editor_set_entity_parent", new() { ["entityId"] = moved, ["parentId"] = target, ["order"] = 0 });
                Assert.True(accepted.GetProperty("accepted").GetBoolean());
                var native = await probe.ReadUntilAsync(engine, packet => packet.GetProperty("Entities").EnumerateArray().Any(entity =>
                    entity.GetProperty("Id").GetInt32() == moved && entity.GetProperty("ParentId").GetInt32() == target && entity.GetProperty("Order").GetInt32() == 0));
                Assert.Equal(new[] { moved }, native.GetProperty("SelectedIds").EnumerateArray().Select(id => id.GetInt32()));
            }
        }
        finally
        {
            await engine.DisposeAsync();
            Assert.Equal(before.ToArray(), (await ExternalProjectCopy.FingerprintAsync(source)).ToArray());
        }
    }

    private static void RestorePreparedProject(string source, string destination)
    {
        var internalDirectory = Path.Combine(source, "Internal");
        if (Directory.Exists(internalDirectory))
            foreach (var meta in Directory.EnumerateFiles(internalDirectory, "*.meta", SearchOption.AllDirectories))
                Restore(meta);
        foreach (var scene in Directory.EnumerateFiles(Path.Combine(source, "Project"), "*.scene", SearchOption.AllDirectories))
            Restore(scene);
        Restore(Path.Combine(source, "Project", "Scripts", "ReiApp.cpp"));

        void Restore(string file)
        {
            var target = Path.Combine(destination, ExternalProjectCopy.RelativeProjectPath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
