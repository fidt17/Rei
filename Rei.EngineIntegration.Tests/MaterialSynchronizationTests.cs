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
                EqualJson(System.Text.Json.JsonSerializer.SerializeToElement(value), state.GetProperty("values").GetProperty("Properties").GetProperty(property));
            }
        }
    });
}
