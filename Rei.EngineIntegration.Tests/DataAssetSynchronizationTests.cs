namespace Rei.EngineIntegration.Tests;

public sealed class DataAssetSynchronizationTests
{
    private const string CONFIG_ID = "9aa55653-f7e5-4d88-bd47-d65ee602504a";

    [EngineFact]
    [Trait("Category", "EngineIntegration")]
    public async Task EditorChangesReachNativeStateAndSurviveOnlyWhenSaved()
    {
        await using var engine = new EngineIntegrationHarness();
        try
        {
            await engine.StartAsync();
            var selected = await engine.CallAsync("rei_editor_select_asset", new() { ["assetId"] = CONFIG_ID });
            Assert.Equal(CONFIG_ID, selected.GetProperty("assetId").GetString());
            Assert.True(selected.GetProperty("monitorSupported").GetBoolean());
            await engine.AssertToolErrorAsync("rei_editor_select_asset", new() { ["assetId"] = "missing" }, "asset_not_found");
            await engine.AssertToolErrorAsync("rei_editor_get_asset_state",
                new() { ["assetId"] = CONFIG_ID, ["source"] = "disk" }, "invalid_source");
            await engine.AssertToolErrorAsync("rei_editor_get_asset_state",
                new() { ["assetId"] = "missing", ["source"] = "runtime" }, "asset_not_found");

            foreach (var source in new[] { "editor", "runtime" })
                Assert.Equal("unsupported", (await engine.ReadAssetAsync("rei_error.rshader", source)).GetProperty("status").GetString());

            // An Editor-loaded asset must not appear loaded in native state as a side effect of inspection.
            var created = await engine.CallAsync("rei_editor_create_data_asset",
                new() { ["typeName"] = "DataAssetTestConfig", ["projectPath"] = "DataAssets/Unloaded.asset" });
            var unloadedId = created.GetProperty("asset").GetProperty("assetId").GetString()!;
            Assert.Equal("loaded", (await engine.ReadAssetAsync(unloadedId, "editor")).GetProperty("status").GetString());
            for (var i = 0; i < 2; i++)
                Assert.Equal("unloaded", (await engine.ReadAssetAsync(unloadedId, "runtime")).GetProperty("status").GetString());

            var baseline = (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty("_floatValue").GetDouble();
            await engine.RunOperationAsync("rei_editor_start_playmode");
            await SetAndCompareFloat(engine, 73.25);
            const string alternateDependency = "b7662265-f06c-43c2-a5f0-bb861c0ab201";
            Assert.Equal("unloaded", (await engine.ReadAssetAsync(alternateDependency, "runtime")).GetProperty("status").GetString());
            await engine.CallAsync("rei_editor_set_data_asset_property", new()
            {
                ["assetId"] = CONFIG_ID, ["propertyName"] = "_dependency",
                ["value"] = new Dictionary<string, object?> { ["Id"] = alternateDependency }
            });
            var nativeDependency = await engine.ReadAssetAsync(alternateDependency, "runtime");
            Assert.Equal("loaded", nativeDependency.GetProperty("status").GetString());
            Assert.Equal(99.25, nativeDependency.GetProperty("values").GetProperty("_value").GetDouble());
            Assert.Equal(alternateDependency, (await engine.ReadAssetAsync(CONFIG_ID, "runtime"))
                .GetProperty("values").GetProperty("_dependency").GetProperty("Id").GetString());

            var label = new string('x', 20000) + " Привет 世界";
            await engine.CallAsync("rei_editor_set_data_asset_property",
                new() { ["assetId"] = CONFIG_ID, ["propertyName"] = "_label", ["value"] = label });
            Assert.Equal(label, (await engine.ReadAssetAsync(CONFIG_ID, "runtime")).GetProperty("values").GetProperty("_label").GetString());
            Assert.Equal(label, (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty("_label").GetString());

            await engine.CallAsync("rei_editor_set_data_asset_property",
                new() { ["assetId"] = CONFIG_ID, ["propertyName"] = "_weights", ["value"] = new[] { 0.125, 2.5, 4.0 } });
            var weights = (await engine.ReadAssetAsync(CONFIG_ID, "runtime")).GetProperty("values").GetProperty("_weights");
            Assert.Equal(new[] { 0.125, 2.5, 4.0 }, weights.EnumerateArray().Select(x => x.GetDouble()));
            var editorWeights = (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty("_weights");
            Assert.Equal(new[] { 0.125, 2.5, 4.0 }, editorWeights.EnumerateArray().Select(x => x.GetDouble()));

            // Stop restores disk values; writing through the Editor alone is not persistence.
            await engine.RunOperationAsync("rei_editor_stop_playmode");
            Assert.Equal(baseline, (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty("_floatValue").GetDouble());
            await SetAndCompareFloat(engine, 31.5);
            await engine.CallAsync("rei_editor_save_project");
            var disk = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(engine.ProjectDirectory, "Project", "DataAssets", "Tests", "DataAssetTestConfig.asset")));
            using (disk) Assert.Equal(31.5, disk.RootElement.GetProperty("SerializedData").GetProperty("_floatValue").GetProperty("Value").GetDouble());

            // Build unloads/reloads the project DLL and its type-erased adapters.
            await engine.RunOperationAsync("rei_editor_start_build", new()
            {
                ["configuration"] = "editor_debug", ["forceSolutionRebuild"] = true
            });
            await engine.RunOperationAsync("rei_editor_start_playmode");
            await AssertFloat(engine, 31.5);
            await SetAndCompareFloat(engine, 62.0);
            await engine.RunOperationAsync("rei_editor_stop_playmode");
            Assert.Equal(31.5, (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty("_floatValue").GetDouble());
        }
        catch (Exception error)
        {
            throw new Exception($"Engine integration failed. Artifacts: {engine.RunDirectory}", error);
        }
    }

    private static async Task SetAndCompareFloat(EngineIntegrationHarness engine, double value)
    {
        // Stop completion precedes automatic EditorMode restart. Wait for native readiness before editing.
        await engine.WaitUntilAsync(async () =>
            (await engine.ReadAssetAsync(CONFIG_ID, "runtime")).GetProperty("status").GetString() == "loaded");
        var changed = await engine.CallAsync("rei_editor_set_data_asset_property",
            new() { ["assetId"] = CONFIG_ID, ["propertyName"] = "_floatValue", ["value"] = value });
        Assert.True(changed.GetProperty("runtimeSynced").GetBoolean());
        await AssertFloat(engine, value);
    }

    private static async Task AssertFloat(EngineIntegrationHarness engine, double expected)
    {
        await engine.WaitUntilAsync(async () =>
        {
            var runtime = await engine.ReadAssetAsync(CONFIG_ID, "runtime");
            return runtime.GetProperty("status").GetString() == "loaded" &&
                Math.Abs(runtime.GetProperty("values").GetProperty("_floatValue").GetDouble() - expected) < 0.00001;
        });
        var editor = await engine.ReadAssetAsync(CONFIG_ID, "editor");
        var native = await engine.ReadAssetAsync(CONFIG_ID, "runtime");
        Assert.Equal(expected, editor.GetProperty("values").GetProperty("_floatValue").GetDouble(), 5);
        Assert.Equal(expected, native.GetProperty("values").GetProperty("_floatValue").GetDouble(), 5);
    }
}
