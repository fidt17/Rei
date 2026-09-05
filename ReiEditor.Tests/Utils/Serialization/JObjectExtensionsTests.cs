using Newtonsoft.Json.Linq;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Tests.Utils.Serialization;

/// <summary>
/// Verifies conversion from JSON tokens into ordinary nested CLR dictionaries, lists, and scalar values.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Serialization")]
public sealed class JObjectExtensionsTests
{
    /// <summary>
    /// Nested objects and arrays retain their keys, order, nulls, and precise CLR scalar types.
    /// </summary>
    [Fact]
    public void ToDictionaryRecursivelyConvertsMixedJson()
    {
        var json = JObject.Parse("""{"items":[{"number":4294967296,"ratio":1.25,"enabled":true,"label":"Ж"},null,[2]],"empty":{}}""");

        var result = json.ToDictionary();

        Assert.Equal(2, result.Count);
        var items = Assert.IsType<List<object?>>(result["items"]);
        Assert.Equal(3, items.Count);
        var item = Assert.IsType<Dictionary<string, object?>>(items[0]);
        Assert.Equal(4294967296L, Assert.IsType<long>(item["number"]));
        Assert.Equal(1.25d, Assert.IsType<double>(item["ratio"]));
        Assert.True(Assert.IsType<bool>(item["enabled"]));
        Assert.Equal("Ж", Assert.IsType<string>(item["label"]));
        Assert.Null(items[1]);
        Assert.Equal(2L, Assert.IsType<long>(Assert.Single(Assert.IsType<List<object?>>(items[2]))));
        Assert.Empty(Assert.IsType<Dictionary<string, object?>>(result["empty"]));
    }

    /// <summary>
    /// Native date tokens retain their DateTime value and UTC kind rather than becoming strings.
    /// </summary>
    [Fact]
    public void ToDictionaryPreservesDateToken()
    {
        var expected = new DateTime(2024, 2, 29, 12, 34, 56, DateTimeKind.Utc);
        var json = new JObject { ["date"] = new JValue(expected) };

        var date = Assert.IsType<DateTime>(json.ToDictionary()["date"]);

        Assert.Equal(expected, date);
        Assert.Equal(DateTimeKind.Utc, date.Kind);
    }

    /// <summary>
    /// Converted containers are detached from source tokens so callers can mutate their data independently.
    /// </summary>
    [Fact]
    public void ToDictionaryDoesNotShareMutableContainersWithSource()
    {
        var json = JObject.Parse("""{"nested":{"name":"original"},"items":[1]}""");
        var result = json.ToDictionary();

        Assert.IsType<Dictionary<string, object?>>(result["nested"])["name"] = "changed";
        Assert.IsType<List<object?>>(result["items"]).Add(2L);

        Assert.Equal("original", json["nested"]!["name"]!.Value<string>());
        Assert.Single(Assert.IsType<JArray>(json["items"]));
    }
}
