using Xunit.Abstractions;
using static Rei.EngineIntegration.Tests.AssetAssertions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Lifecycle")]
[Trait("Area", "DataAssets")]
public sealed class AssetLifecycleTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task SavedValuesSurviveDllRebuildAndPlayChangesRollBack()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        await SetAsync(engine, "_floatValue", 31.5);
        await AssertPropertyAsync(engine, "_floatValue", 31.5);
        await engine.CallAsync("rei_editor_save_project");
        await AssertDiskFloatAsync(engine, 31.5);

        await engine.RunOperationAsync("rei_editor_start_build", new()
        {
            ["configuration"] = "editor_debug", ["forceSolutionRebuild"] = true
        });
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await WaitForConfigAsync(engine);
        await AssertPropertyAsync(engine, "_floatValue", 31.5);
        await SetAsync(engine, "_floatValue", 62.0);
        await AssertPropertyAsync(engine, "_floatValue", 62.0);
        await AssertDiskFloatAsync(engine, 31.5);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForConfigAsync(engine);
        await AssertPropertyAsync(engine, "_floatValue", 31.5);
        Assert.Equal(1, engine.LaunchCount);
    }

    [EngineFact]
    public async Task ProcessRestartKeepsSavedValuesAndDiscardsUnsavedValues()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        await SetAsync(engine, "_floatValue", 41.25);
        await engine.CallAsync("rei_editor_save_project");
        await AssertDiskFloatAsync(engine, 41.25);
        await SetAsync(engine, "_floatValue", 88.5);
        await AssertPropertyAsync(engine, "_floatValue", 88.5);
        await AssertDiskFloatAsync(engine, 41.25);

        await engine.RestartAsync();
        await WaitForConfigAsync(engine);
        Assert.Equal(2, engine.LaunchCount);
        await AssertPropertyAsync(engine, "_floatValue", 41.25);
        await AssertDiskFloatAsync(engine, 41.25);
        // A new process must also retain functioning adapters, not merely load the saved snapshot.
        await SetAsync(engine, "_floatValue", 63.75);
        await AssertPropertyAsync(engine, "_floatValue", 63.75);
    }

    [EngineFact]
    public async Task NewlyCreatedAssetRemainsUnloadedDuringInspection()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        var created = await engine.CallAsync("rei_editor_create_data_asset",
            new() { ["typeName"] = "DataAssetTestConfig", ["projectPath"] = "DataAssets/Unloaded.asset" });
        var id = created.GetProperty("asset").GetProperty("assetId").GetString()!;
        Assert.Equal("loaded", (await engine.ReadAssetAsync(id, "editor")).GetProperty("status").GetString());
        for (var i = 0; i < 2; i++)
            Assert.Equal("unloaded", (await engine.ReadAssetAsync(id, "runtime")).GetProperty("status").GetString());
    }
}
