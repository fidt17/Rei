using System.Text.Json;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "Time")]
[Trait("Suite", "Lifecycle")]
public sealed class TimeLifecycleTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task FrameClockAdvancesConsistentlyAndResetsAcrossPlayStop()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        var created = await engine.CallAsync("rei_editor_create_data_asset", new()
        {
            ["typeName"] = "FrameTimeProbeData", ["projectPath"] = "DataAssets/FrameTimeProbe.asset"
        });
        var assetId = created.GetProperty("asset").GetProperty("assetId").GetString()!;
        var entities = await engine.CallAsync("rei_editor_list_entities");
        var entityId = entities.GetProperty("entities")[0].GetProperty("id").GetInt32();
        await engine.CallAsync("rei_editor_add_behaviour", new()
        {
            ["entityId"] = entityId, ["behaviourName"] = "FrameTimeProbe"
        });
        await engine.CallAsync("rei_editor_set_behaviour_property", new()
        {
            ["entityId"] = entityId, ["behaviourName"] = "FrameTimeProbe", ["propertyName"] = "_data",
            ["value"] = new Dictionary<string, object?> { ["Id"] = assetId }
        });
        await engine.CallAsync("rei_editor_save_project");
        var diskPath = Path.Combine(engine.ProjectDirectory, "Project", "DataAssets", "FrameTimeProbe.asset");
        var persisted = await File.ReadAllBytesAsync(diskPath);
        for (var cycle = 1; cycle <= 3; cycle++)
        {
            output.WriteLine($"Time lifecycle {cycle}");
            await engine.RunOperationAsync("rei_editor_start_playmode");
            await engine.WaitUntilAsync(async () =>
            {
                var state = await engine.ReadAssetAsync(assetId, "runtime");
                return state.GetProperty("status").GetString() == "loaded" &&
                    state.GetProperty("values").GetProperty("FrameCount").GetInt32() >= 5;
            });
            var first = (await engine.ReadAssetAsync(assetId, "runtime")).GetProperty("values");
            AssertClock(first);
            await engine.WaitUntilAsync(async () =>
                (await engine.ReadAssetAsync(assetId, "runtime")).GetProperty("values").GetProperty("Elapsed").GetDouble() >
                first.GetProperty("Elapsed").GetDouble());
            AssertClock((await engine.ReadAssetAsync(assetId, "runtime")).GetProperty("values"));
            await engine.RunOperationAsync("rei_editor_stop_playmode");
            await engine.WaitUntilAsync(async () =>
            {
                var state = await engine.ReadAssetAsync(assetId, "runtime");
                return state.GetProperty("status").GetString() == "loaded" &&
                    state.GetProperty("values").GetProperty("FrameCount").GetInt32() == 0;
            });
            Assert.Equal(persisted, await File.ReadAllBytesAsync(diskPath));
        }
        var errors = await engine.CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "error", ["limit"] = 500 });
        Assert.Empty(errors.GetProperty("entries").EnumerateArray());
    }

    private static void AssertClock(JsonElement values)
    {
        Assert.Equal(0, values.GetProperty("FirstDelta").GetDouble());
        Assert.True(values.GetProperty("Delta").GetDouble() > 0);
        Assert.True(values.GetProperty("Elapsed").GetDouble() > 0);
        Assert.True(values.GetProperty("SessionProgress").GetDouble() > 0);
        Assert.Equal(0, values.GetProperty("InvalidFrames").GetInt32());
        Assert.Equal(0, values.GetProperty("UnstableReads").GetInt32());
    }
}
