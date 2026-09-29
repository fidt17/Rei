using System.Text.Json;

namespace Rei.EngineIntegration.Tests;

internal static class AssetAssertions
{
    public const string CONFIG_ID = "9aa55653-f7e5-4d88-bd47-d65ee602504a";
    public const string ALTERNATE_ID = "b7662265-f06c-43c2-a5f0-bb861c0ab201";
    public const string MATERIAL_ID = "rei_simple_lit.mat";

    public static Task<JsonElement> SetAsync(EngineIntegrationHarness engine, string property, object? value, string assetId = CONFIG_ID) =>
        engine.CallAsync("rei_editor_set_data_asset_property", new() { ["assetId"] = assetId, ["propertyName"] = property, ["value"] = JsonSerializer.SerializeToElement(value) });

    public static async Task WaitForConfigAsync(EngineIntegrationHarness engine) =>
        await engine.WaitUntilAsync(async () => (await engine.ReadAssetAsync(CONFIG_ID, "runtime")).GetProperty("status").GetString() == "loaded");

    public static async Task AssertPropertyAsync(EngineIntegrationHarness engine, string property, object? expected, string assetId = CONFIG_ID)
    {
        var expectedJson = JsonSerializer.SerializeToElement(expected);
        foreach (var source in new[] { "editor", "runtime" })
        {
            var state = await engine.ReadAssetAsync(assetId, source);
            Assert.Equal("loaded", state.GetProperty("status").GetString());
            EqualJson(expectedJson, state.GetProperty("values").GetProperty(property));
        }
    }

    public static void EqualJson(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                // Native serialization adds type metadata; it is not an editable asset value.
                Assert.Equal(ValueProperties(expected).Select(p => p.Name).Order(), ValueProperties(actual).Select(p => p.Name).Order());
                foreach (var property in ValueProperties(expected)) EqualJson(property.Value, actual.GetProperty(property.Name));
                break;
            case JsonValueKind.Array:
                Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
                foreach (var (left, right) in expected.EnumerateArray().Zip(actual.EnumerateArray())) EqualJson(left, right);
                break;
            case JsonValueKind.Number:
                if (expected.TryGetInt64(out var expectedInteger) && actual.TryGetInt64(out var actualInteger))
                {
                    Assert.Equal(expectedInteger, actualInteger);
                    break;
                }
                var tolerance = Math.Max(0.00001, Math.Abs(expected.GetDouble()) * 0.000001);
                Assert.InRange(Math.Abs(expected.GetDouble() - actual.GetDouble()), 0, tolerance);
                break;
            default:
                Assert.Equal(expected.ToString(), actual.ToString());
                break;
        }
    }

    private static IEnumerable<JsonProperty> ValueProperties(JsonElement element) => element.EnumerateObject().Where(property => property.Name != "REI_TYPE");

    public static async Task AssertDiskFloatAsync(EngineIntegrationHarness engine, double expected)
    {
        using var disk = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.ProjectDirectory,
            "Project", "DataAssets", "Tests", "DataAssetTestConfig.asset")));
        Assert.Equal(expected, disk.RootElement.GetProperty("SerializedData").GetProperty("_floatValue").GetProperty("Value").GetDouble(), 5);
    }
}
