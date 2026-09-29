using System.Text.Json;
using Xunit.Abstractions;
using static Rei.EngineIntegration.Tests.AssetAssertions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Smoke")]
[Trait("Area", "DataAssets")]
public sealed class DataAssetSynchronizationTests(SharedEngineFixture fixture, ITestOutputHelper output)
{
    [EngineFact]
    public Task SelectionAndInspectionValidateRequests() => Run(nameof(SelectionAndInspectionValidateRequests), async engine =>
    {
        var selected = await engine.CallAsync("rei_editor_select_asset", new() { ["assetId"] = CONFIG_ID });
        Assert.Equal(CONFIG_ID, selected.GetProperty("assetId").GetString());
        Assert.True(selected.GetProperty("monitorSupported").GetBoolean());
        await engine.AssertToolErrorAsync("rei_editor_select_asset", new() { ["assetId"] = "missing" }, "asset_not_found");
        await engine.AssertToolErrorAsync("rei_editor_get_asset_state", new() { ["assetId"] = CONFIG_ID, ["source"] = "disk" }, "invalid_source");
        await engine.AssertToolErrorAsync("rei_editor_get_asset_state", new() { ["assetId"] = "missing", ["source"] = "runtime" }, "asset_not_found");
        foreach (var source in new[] { "editor", "runtime" })
            Assert.Equal("unsupported", (await engine.ReadAssetAsync("rei_error.rshader", source)).GetProperty("status").GetString());
    });

    [EngineFact]
    public Task ScalarsAndEnumReachNativeState() => Run(nameof(ScalarsAndEnumReachNativeState), async engine =>
    {
        foreach (var (property, value) in new (string, object)[] {
            ("_enabled", false), ("_signedValue", -123), ("_unsignedValue", 456),
            ("_floatValue", 73.25), ("_mode", 2) })
        {
            await SetAsync(engine, property, value);
            await AssertPropertyAsync(engine, property, value);
        }
    });

    [EngineFact]
    public Task VectorsAndColorReachNativeState() => Run(nameof(VectorsAndColorReachNativeState), async engine =>
    {
        foreach (var (property, value) in new (string, object)[] {
            ("_position2D", new { x = -1.25, y = 8.5 }),
            ("_position3D", new { x = 3.25, y = -9.5, z = 0.125 }),
            ("_tint", new { r = 0.25, g = 0.5, b = 0.75, a = 1.0 }) })
        {
            await SetAsync(engine, property, value);
            await AssertPropertyAsync(engine, property, value);
        }
    });

    [EngineFact]
    public Task LargeUnicodeAndCollectionResizeReachNativeState() => Run(nameof(LargeUnicodeAndCollectionResizeReachNativeState), async engine =>
    {
        var label = new string('x', 20000) + " Привет 世界";
        await SetAsync(engine, "_label", label);
        await AssertPropertyAsync(engine, "_label", label);
        foreach (var weights in new[] { new[] { 0.125, 2.5, 4.0, 8.0 }, Array.Empty<double>(), new[] { 0.5 } })
        {
            await SetAsync(engine, "_weights", weights);
            await AssertPropertyAsync(engine, "_weights", weights);
        }
    });

    [EngineFact]
    public Task DependencyReplacementLoadsNativeAsset() => Run(nameof(DependencyReplacementLoadsNativeAsset), async engine =>
    {
        Assert.Equal("unloaded", (await engine.ReadAssetAsync(ALTERNATE_ID, "runtime")).GetProperty("status").GetString());
        await SetAsync(engine, "_dependency", new { Id = ALTERNATE_ID });
        await AssertPropertyAsync(engine, "_dependency", new { Id = ALTERNATE_ID });
        await AssertPropertyAsync(engine, "_value", 99.25, ALTERNATE_ID);
    });

    [EngineFact]
    public Task UnloadedAssetInspectionAndEditingDoNotLoadNativeAsset() => Run(nameof(UnloadedAssetInspectionAndEditingDoNotLoadNativeAsset), async engine =>
    {
        Assert.Equal("loaded", (await engine.ReadAssetAsync(ALTERNATE_ID, "editor")).GetProperty("status").GetString());
        for (var i = 0; i < 2; i++)
            Assert.Equal("unloaded", (await engine.ReadAssetAsync(ALTERNATE_ID, "runtime")).GetProperty("status").GetString());
        var result = await SetAsync(engine, "_value", 47.5, ALTERNATE_ID);
        Assert.False(result.GetProperty("runtimeSynced").GetBoolean());
        Assert.Equal(47.5, (await engine.ReadAssetAsync(ALTERNATE_ID, "editor")).GetProperty("values").GetProperty("_value").GetDouble());
        Assert.Equal("unloaded", (await engine.ReadAssetAsync(ALTERNATE_ID, "runtime")).GetProperty("status").GetString());
    });

    [EngineFact]
    public Task RejectedReferencesAndValuesLeaveBothStatesUnchanged() => Run(nameof(RejectedReferencesAndValuesLeaveBothStatesUnchanged), async engine =>
    {
        var editorBefore = (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values");
        var nativeBefore = (await engine.ReadAssetAsync(CONFIG_ID, "runtime")).GetProperty("values");
        foreach (var (property, value) in new (string, object)[] {
            ("_dependency", new { Id = CONFIG_ID }), ("_dependency", new { Id = "missing" }),
            ("_material", new { Id = ALTERNATE_ID }), ("_floatValue", "not a number") })
        {
            await engine.AssertToolErrorAsync("rei_editor_set_data_asset_property",
                new() { ["assetId"] = CONFIG_ID, ["propertyName"] = property, ["value"] = JsonSerializer.SerializeToElement(value) }, "invalid_property_value");
            EqualJson(editorBefore, (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values"));
            EqualJson(nativeBefore, (await engine.ReadAssetAsync(CONFIG_ID, "runtime")).GetProperty("values"));
        }
    });

    private Task Run(string name, Func<EngineIntegrationHarness, Task> test) => fixture.RunAsync(name, output, test);
}
