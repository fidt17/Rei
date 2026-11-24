using System.Text.Json;
using Xunit.Abstractions;
using static Rei.EngineIntegration.Tests.AssetAssertions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Smoke")]
[Trait("Area", "Materials")]
public sealed class MaterialSynchronizationTests(SharedEngineFixture fixture, ITestOutputHelper output)
{
    [EngineFact]
    public Task MaterialUniformsReachNativeState() => fixture.RunAsync(nameof(MaterialUniformsReachNativeState), output, async engine =>
    {
        await SetAsync(engine, "_material", new { Id = MATERIAL_ID });
        await AssertPropertyAsync(engine, "_material", new { Id = MATERIAL_ID });
        foreach (var (property, value) in new (string, object)[] {
            ("_Shininess", 16.5), ("_Color", new { r = 0.25, g = 0.5, b = 0.75, a = 1.0 }) })
        {
            await engine.CallAsync("rei_editor_set_material_property",
                new() { ["materialAssetId"] = MATERIAL_ID, ["propertyName"] = property, ["value"] = value });
            foreach (var source in new[] { "editor", "runtime" })
            {
                var state = await engine.ReadAssetAsync(MATERIAL_ID, source);
                Assert.Equal("loaded", state.GetProperty("status").GetString());
                EqualJson(JsonSerializer.SerializeToElement(value), state.GetProperty("values").GetProperty("Properties").GetProperty(property));
            }
        }
    });
}

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Lifecycle")]
[Trait("Area", "Materials")]
public sealed class MaterialHistoryTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task MaterialUniformUndoRedoKeepsOtherValuesInEditorAndRuntime()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        await SetAsync(engine, "_material", new { Id = MATERIAL_ID });
        await AssertPropertyAsync(engine, "_material", new { Id = MATERIAL_ID });
        var selected = await engine.CallAsync("rei_editor_select_asset", new() { ["assetId"] = MATERIAL_ID });
        Assert.True(selected.GetProperty("monitorSupported").GetBoolean());
        var original = (await engine.ReadAssetAsync(MATERIAL_ID, "editor")).GetProperty("values").GetProperty("Properties");
        var baseShininess = original.GetProperty("_Shininess").GetDouble();
        var baseColor = original.GetProperty("_Color");
        object? baseTexture = original.TryGetProperty("_MainTex", out var texture) ? texture : null;
        var newColor = new { r = 0.25, g = 0.5, b = 0.75, a = 0.5 };
        var newTexture = new { Id = "rei_test_texture.png" };
        var emptyTexture = new { Id = "" };

        await AssertState(baseShininess, baseColor, baseTexture);
        await SetUniform("_Shininess", 16.5);
        await AssertState(16.5, baseColor, baseTexture);
        await SetUniform("_Color", newColor);
        await AssertState(16.5, newColor, baseTexture);
        await SetUniform("_MainTex", newTexture);
        await AssertState(16.5, newColor, newTexture);
        await SetUniform("_MainTex", emptyTexture);
        await AssertState(16.5, newColor, emptyTexture);

        await engine.CallAsync("rei_editor_undo");
        await AssertState(16.5, newColor, newTexture);
        await engine.CallAsync("rei_editor_undo");
        await AssertState(16.5, newColor, baseTexture);
        await engine.CallAsync("rei_editor_undo");
        await AssertState(16.5, baseColor, baseTexture);
        await engine.CallAsync("rei_editor_undo");
        await AssertState(baseShininess, baseColor, baseTexture);
        await engine.CallAsync("rei_editor_redo");
        await AssertState(16.5, baseColor, baseTexture);
        await engine.CallAsync("rei_editor_redo");
        await AssertState(16.5, newColor, baseTexture);
        await engine.CallAsync("rei_editor_redo");
        await AssertState(16.5, newColor, newTexture);
        await engine.CallAsync("rei_editor_redo");
        await AssertState(16.5, newColor, emptyTexture);
        Assert.False((await engine.CallAsync("rei_editor_get_command_state")).GetProperty("canRedo").GetBoolean());

        async Task SetUniform(string property, object value)
        {
            var result = await engine.CallAsync("rei_editor_set_material_property",
                new() { ["materialAssetId"] = MATERIAL_ID, ["propertyName"] = property, ["value"] = JsonSerializer.SerializeToElement(value) });
            Assert.True(result.GetProperty("changed").GetBoolean());
        }

        async Task AssertState(double shininess, object color, object? textureValue)
        {
            foreach (var source in new[] { "editor", "runtime" })
            {
                var state = await engine.ReadAssetAsync(MATERIAL_ID, source);
                Assert.Equal("loaded", state.GetProperty("status").GetString());
                var properties = state.GetProperty("values").GetProperty("Properties");
                EqualJson(JsonSerializer.SerializeToElement(shininess), properties.GetProperty("_Shininess"));
                EqualJson(JsonSerializer.SerializeToElement(color), properties.GetProperty("_Color"));
                if (textureValue == null) Assert.False(properties.TryGetProperty("_MainTex", out _));
                else EqualJson(JsonSerializer.SerializeToElement(textureValue), properties.GetProperty("_MainTex"));
            }
        }
    }
}
