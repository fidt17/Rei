using System.Text.Json;
using Xunit.Sdk;

namespace Rei.EngineIntegration.Tests;

public sealed class AssetAssertionsTests
{
    [Fact]
    public void ValueComparisonAllowsNativeMetadataAndFloatPrecision()
    {
        AssetAssertions.EqualJson(JsonSerializer.SerializeToElement(new { x = 0.1 }),
            JsonSerializer.SerializeToElement(new { REI_TYPE = 123, x = 0.10000000149 }));
    }

    [Theory]
    [InlineData("{\"x\":1}", "{\"x\":2}")]
    [InlineData("{\"x\":1}", "{\"x\":1,\"extra\":2}")]
    [InlineData("[1,2]", "[1]")]
    [InlineData("{\"Id\":\"original\"}", "{\"Id\":\"different\"}")]
    [InlineData("0.1", "0.101")]
    public void ValueComparisonRejectsDifferentValues(string expected, string actual)
    {
        using var left = JsonDocument.Parse(expected);
        using var right = JsonDocument.Parse(actual);
        Assert.ThrowsAny<XunitException>(() => AssetAssertions.EqualJson(left.RootElement, right.RootElement));
    }
}
