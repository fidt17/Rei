using System.Diagnostics;
using System.Text.Json;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Benchmark")]
[Trait("Area", "EditorInteraction")]
public sealed class EditorInteractionBenchmarkTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task MeasureEditorSelectionNativeSelectionAndReparent()
    {
        if (Environment.GetEnvironmentVariable("REI_RUN_EDITOR_BENCHMARKS") != "1") return;
        var source = Environment.GetEnvironmentVariable("REI_INTERACTION_PROJECT");
        foreach (var size in source == null ? new[] { 0, 100 } : new[] { 0 })
        {
            var preparedRun = Environment.GetEnvironmentVariable("REI_INTERACTION_PREPARED_RUN");
            await using var engine = new EngineIntegrationHarness(sourceProjectDirectory: source, preparedRunDirectory: preparedRun);
            var before = source == null ? null : await ExternalProjectCopy.FingerprintAsync(source);
            output.WriteLine($"Artifacts: {engine.RunDirectory}");
            await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
            if (preparedRun != null && source != null)
            {
                File.Copy(Path.Combine(source, "Project", "Scripts", "ReiApp.cpp"), Path.Combine(engine.ProjectDirectory, "Project", "Scripts", "ReiApp.cpp"), overwrite: true);
                await ExternalProjectCopy.LocalizeBuildFilesAsync(source, engine.ProjectDirectory);
            }
            await SceneSynchronizationTests.PrepareProbeAsync(engine.ProjectDirectory, size, preserveApplication: source != null);
            File.Delete(Path.Combine(engine.ProjectDirectory, "scene-sync-command.json"));
            File.Delete(Path.Combine(engine.ProjectDirectory, "scene-sync-state.json"));
            var oldTrace = Environment.GetEnvironmentVariable("REI_EDITOR_INTERACTION_TRACE");
            Environment.SetEnvironmentVariable("REI_EDITOR_INTERACTION_TRACE", Path.Combine(engine.RunDirectory, "editor-interaction.jsonl"));
            try
            {
                await engine.StartAsync();
                var probe = new SceneSynchronizationTests(output);
                var initial = await probe.CommandAsync(engine, "read");
                var entities = initial.GetProperty("Entities").EnumerateArray().ToArray();
                var ids = entities.Select(entity => entity.GetProperty("Id").GetInt32()).ToArray();
                Assert.True(ids.Length >= 2);
                var trials = new List<object>();
                // Let startup layout and JIT settle; warmup samples are identified separately.
                await Task.Delay(1000);
                await engine.CallAsync("rei_editor_start_profiling_capture", new() { ["frameCount"] = 120 });
                var selectionTrials = Environment.GetEnvironmentVariable("REI_INTERACTION_REPARENT_ONLY") == "1" ? 0 : 24;
                for (var i = 0; i < selectionTrials; i++)
                {
                    var id = ids[i % ids.Length];
                    var start = Stopwatch.GetTimestamp();
                    await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { id } });
                    var end = Stopwatch.GetTimestamp();
                    var runtime = await engine.CallAsync("rei_editor_get_selection_state", new() { ["source"] = "runtime" });
                    Assert.Equal(new[] { id }, runtime.GetProperty("selectedIds").EnumerateArray().Select(value => value.GetInt32()));
                    trials.Add(new { kind = "editor-selection", id, warmup = i < 4, start, end });
                    await Task.Delay(120);
                }
                await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = Array.Empty<int>() });
                for (var i = 0; i < selectionTrials; i++)
                {
                    var id = ids[i % ids.Length];
                    var native = await probe.CommandAsync(engine, "select", ("Ids", new[] { id }));
                    trials.Add(new { kind = "native-selection", id, warmup = i < 4, nativeTicks = native.GetProperty("Ticks").GetInt64() });
                    await Task.Delay(120);
                }
                if (selectionTrials == 0)
                {
                    await engine.CallAsync("rei_editor_set_entity_selection", new() { ["entityIds"] = new[] { ids[Math.Min(23, ids.Length - 1)] } });
                    await Task.Delay(1000);
                }
                var parentIds = entities.Select(entity => entity.GetProperty("ParentId").GetInt32()).ToHashSet();
                var moved = ids.Last(id => !parentIds.Contains(id));
                var targetParent = ids.First(id => id != moved);
                await engine.AssertToolErrorAsync("rei_editor_set_entity_parent", new() { ["entityId"] = moved, ["parentId"] = moved, ["order"] = 0 }, "invalid_parent");
                await engine.AssertToolErrorAsync("rei_editor_set_entity_parent", new() { ["entityId"] = moved, ["parentId"] = 0, ["order"] = -1 }, "invalid_order");
                await engine.AssertToolErrorAsync("rei_editor_set_entity_parent", new() { ["entityId"] = int.MaxValue, ["parentId"] = 0, ["order"] = 0 }, "entity_not_found");
                for (var i = 0; i < 16; i++)
                {
                    var parent = i % 2 == 0 ? targetParent : 0;
                    var start = Stopwatch.GetTimestamp();
                    var accepted = await engine.CallAsync("rei_editor_set_entity_parent", new() { ["entityId"] = moved, ["parentId"] = parent, ["order"] = 0 });
                    var end = Stopwatch.GetTimestamp();
                    Assert.True(accepted.GetProperty("accepted").GetBoolean());
                    var native = await probe.ReadUntilAsync(engine, packet => packet.GetProperty("Entities").EnumerateArray().Any(entity =>
                        entity.GetProperty("Id").GetInt32() == moved && entity.GetProperty("ParentId").GetInt32() == parent && entity.GetProperty("Order").GetInt32() == 0));
                    Assert.Equal(parent, native.GetProperty("Entities").EnumerateArray().Single(entity => entity.GetProperty("Id").GetInt32() == moved).GetProperty("ParentId").GetInt32());
                    if (parent != 0)
                        await engine.AssertToolErrorAsync("rei_editor_set_entity_parent", new() { ["entityId"] = parent, ["parentId"] = moved, ["order"] = 0 }, "invalid_parent");
                    trials.Add(new { kind = "editor-reparent", id = moved, parent, warmup = i < 4, start, end });
                    await Task.Delay(120);
                }
                await Task.Delay(1200);
                var profile = await engine.CallAsync("rei_editor_get_profiling_snapshot", new() { ["view"] = "last_capture" });
                await File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, "native-interaction-profile.json"), profile.GetRawText());
                await File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, "interaction-trials.json"), JsonSerializer.Serialize(new
                {
                    commit = "059d808", configuration = "Debug / EditorDebug", frequency = Stopwatch.Frequency,
                    source = source ?? "DataAssets", entityCount = ids.Length, trials
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            finally
            {
                Environment.SetEnvironmentVariable("REI_EDITOR_INTERACTION_TRACE", oldTrace);
                await engine.DisposeAsync();
                if (source != null) Assert.Equal(before!.ToArray(), (await ExternalProjectCopy.FingerprintAsync(source)).ToArray());
            }
        }
    }
}
