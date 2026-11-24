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
    public async Task NumericPropertyUndoRedoUpdatesEditorAndRuntimeBeforeSave()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        const string property = "_floatValue";
        var before = (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty(property).GetDouble();
        var after = before + 1.25;
        await AssertPropertyAsync(engine, property, before);

        await SetAsync(engine, property, after);
        await AssertPropertyAsync(engine, property, after);
        Assert.False((await SetAsync(engine, property, after)).GetProperty("changed").GetBoolean());
        await engine.CallAsync("rei_editor_undo");
        await AssertPropertyAsync(engine, property, before);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.CallAsync("rei_editor_redo");
        await AssertPropertyAsync(engine, property, after);

        await AssertDiskFloatAsync(engine, before);
        await engine.CallAsync("rei_editor_save_project");
        await AssertDiskFloatAsync(engine, after);

        const string rangeProperty = "_signedValue";
        var originalRangeValue = (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty(rangeProperty).GetInt32();
        const int editedRangeValue = 45;
        await SetAsync(engine, rangeProperty, editedRangeValue);
        await AssertPropertyAsync(engine, rangeProperty, editedRangeValue);
        await engine.CallAsync("rei_editor_undo");
        await AssertPropertyAsync(engine, rangeProperty, originalRangeValue);
        await engine.CallAsync("rei_editor_redo");
        await AssertPropertyAsync(engine, rangeProperty, editedRangeValue);
    }

    [EngineFact]
    public async Task CheckboxEnumAndTextReplayInOrderInEditorAndRuntime()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        var before = await ReadState("editor");
        var enabled = !before.Enabled;
        var mode = before.Mode == 2 ? 1 : 2;
        const string label = "Undo/Redo — DataAsset";
        var edits = new (string Property, object Value)[] { ("_enabled", enabled), ("_mode", mode), ("_label", label) };
        var states = new[] { before, (enabled, before.Mode, before.Label), (enabled, mode, before.Label), (enabled, mode, label) };

        await AssertState(states[0]);
        for (var i = 0; i < edits.Length; i++)
        {
            await SetAsync(engine, edits[i].Property, edits[i].Value);
            await AssertState(states[i + 1]);
        }
        for (var i = edits.Length - 1; i >= 0; i--)
        {
            await engine.CallAsync("rei_editor_undo");
            await AssertState(states[i]);
        }
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        for (var i = 1; i < states.Length; i++)
        {
            await engine.CallAsync("rei_editor_redo");
            await AssertState(states[i]);
        }
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());

        async Task<(bool Enabled, int Mode, string Label)> ReadState(string source)
        {
            var state = await engine.ReadAssetAsync(CONFIG_ID, source);
            Assert.Equal("loaded", state.GetProperty("status").GetString());
            var values = state.GetProperty("values");
            return (values.GetProperty("_enabled").GetBoolean(), values.GetProperty("_mode").GetInt32(),
                values.GetProperty("_label").GetString()!);
        }

        async Task AssertState((bool, int, string) expected)
        {
            Assert.Equal(expected, await ReadState("editor"));
            Assert.Equal(expected, await ReadState("runtime"));
        }
    }

    [EngineFact]
    public async Task VectorCoordinateUndoRedoPreservesOtherAxesInEditorAndRuntime()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        const string vector2 = "_position2D";
        const string vector3 = "_position3D";
        var properties = (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values");
        var original2 = properties.GetProperty(vector2);
        var original3 = properties.GetProperty(vector3);
        var updated2 = new { x = 5.75f, y = original2.GetProperty("y").GetDouble() };
        var updated3 = new { x = original3.GetProperty("x").GetDouble(), y = original3.GetProperty("y").GetDouble(), z = -10.5f };

        await AssertVectors(original2, original3);
        await SetAsync(engine, vector2, new { updated2.x });
        await AssertVectors(updated2, original3);
        await SetAsync(engine, vector3, new { updated3.z });
        await AssertVectors(updated2, updated3);

        await engine.CallAsync("rei_editor_undo");
        await AssertVectors(updated2, original3);
        await engine.CallAsync("rei_editor_undo");
        await AssertVectors(original2, original3);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.CallAsync("rei_editor_redo");
        await AssertVectors(updated2, original3);
        await engine.CallAsync("rei_editor_redo");
        await AssertVectors(updated2, updated3);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());

        async Task AssertVectors(object expected2, object expected3)
        {
            await AssertPropertyAsync(engine, vector2, expected2);
            await AssertPropertyAsync(engine, vector3, expected3);
        }
    }

    [EngineFact]
    public async Task ColorUndoRedoPreservesUntouchedChannelsInEditorAndRuntime()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        const string property = "_tint";
        var original = (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty(property);
        var afterRed = new { r = 0.25, g = original.GetProperty("g").GetDouble(), b = original.GetProperty("b").GetDouble(), a = original.GetProperty("a").GetDouble() };
        var afterAlpha = afterRed with { a = 0.375 };

        await AssertPropertyAsync(engine, property, original);
        await SetAsync(engine, property, new { afterRed.r });
        await AssertPropertyAsync(engine, property, afterRed);
        await SetAsync(engine, property, new { afterAlpha.a });
        await AssertPropertyAsync(engine, property, afterAlpha);

        await engine.CallAsync("rei_editor_undo");
        await AssertPropertyAsync(engine, property, afterRed);
        await engine.CallAsync("rei_editor_undo");
        await AssertPropertyAsync(engine, property, original);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.CallAsync("rei_editor_redo");
        await AssertPropertyAsync(engine, property, afterRed);
        await engine.CallAsync("rei_editor_redo");
        await AssertPropertyAsync(engine, property, afterAlpha);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());
    }

    [EngineFact]
    public async Task AssetReferenceSelectionAndClearUndoRedoPreserveIdsInEditorAndRuntime()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        const string property = "_dependency";
        var original = (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty(property);
        var selected = new { Id = ALTERNATE_ID };
        var cleared = new { Id = "" };

        await AssertPropertyAsync(engine, property, original);
        await SetAsync(engine, property, selected);
        await AssertPropertyAsync(engine, property, selected);
        await SetAsync(engine, property, cleared);
        await AssertPropertyAsync(engine, property, cleared);

        await engine.CallAsync("rei_editor_undo");
        await AssertPropertyAsync(engine, property, selected);
        await engine.CallAsync("rei_editor_undo");
        await AssertPropertyAsync(engine, property, original);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.CallAsync("rei_editor_redo");
        await AssertPropertyAsync(engine, property, selected);
        await engine.CallAsync("rei_editor_redo");
        await AssertPropertyAsync(engine, property, cleared);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());
    }

    [EngineFact]
    public async Task NestedFieldUndoRedoPreservesSiblingInEditorAndRuntime()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        const string property = "_settings";
        var original = new { Value = 1.25, Label = "Before" };
        var afterValue = original with { Value = 3.5 };
        var afterLabel = afterValue with { Label = "After" };

        await AssertPropertyAsync(engine, property, original);
        await SetAsync(engine, property, new { afterValue.Value });
        await AssertPropertyAsync(engine, property, afterValue);
        await SetAsync(engine, property, new { afterLabel.Label });
        await AssertPropertyAsync(engine, property, afterLabel);

        await engine.CallAsync("rei_editor_undo");
        await AssertPropertyAsync(engine, property, afterValue);
        await engine.CallAsync("rei_editor_undo");
        await AssertPropertyAsync(engine, property, original);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await engine.CallAsync("rei_editor_redo");
        await AssertPropertyAsync(engine, property, afterValue);
        await engine.CallAsync("rei_editor_redo");
        await AssertPropertyAsync(engine, property, afterLabel);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());
    }

    [EngineFact]
    public async Task CollectionEditAddAndRemoveUndoRedoPreserveValuesAndOrderInEditorAndRuntime()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        const string property = "_weights";
        double[] original = [0.25, 0.5, 1];
        double[] edited = [0.25, 2.5, 1];
        double[] added = [0.25, 2.5, 1, 0];
        double[] removed = [0.25, 1, 0];

        await AssertPropertyAsync(engine, property, original);
        foreach (var expected in new[] { edited, added, removed })
        {
            await SetAsync(engine, property, expected);
            await AssertPropertyAsync(engine, property, expected);
        }
        foreach (var expected in new[] { added, edited, original })
        {
            await engine.CallAsync("rei_editor_undo");
            await AssertPropertyAsync(engine, property, expected);
        }
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        foreach (var expected in new[] { edited, added, removed })
        {
            await engine.CallAsync("rei_editor_redo");
            await AssertPropertyAsync(engine, property, expected);
        }
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());
    }

    [EngineFact]
    public async Task DataAssetHistorySurvivesDllRebuildAndPlayChangesRollBack()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        var original = (await engine.ReadAssetAsync(CONFIG_ID, "editor")).GetProperty("values").GetProperty("_floatValue").GetDouble();
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
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canUndo").GetBoolean());
        await AssertDiskFloatAsync(engine, 31.5);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForConfigAsync(engine);
        await AssertPropertyAsync(engine, "_floatValue", 31.5);
        await engine.CallAsync("rei_editor_undo");
        await AssertPropertyAsync(engine, "_floatValue", original);
        await engine.CallAsync("rei_editor_redo");
        await AssertPropertyAsync(engine, "_floatValue", 31.5);
        await AssertDiskFloatAsync(engine, 31.5);
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
