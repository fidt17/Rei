using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Meta;

namespace ReiEditor.Tests.Models.Services.Assets.Meta;

/// <summary>
/// Verifies typed metadata storage, JSON conversion, and copy behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Assets")]
public sealed class AssetMetaTests
{
    /// <summary>
    /// Provides mutable metadata for typed conversion and shallow-copy checks.
    /// </summary>
    private sealed class TestMetadata
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }

        public TestMetadata()
        {
        }
    }

    /// <summary>
    /// Stored typed value can be retrieved through direct and try-get APIs.
    /// </summary>
    [Fact]
    public void TypedDataRoundTrips()
    {
        var meta = new AssetMeta("asset");
        var expected = new TestMetadata { Name = "surface", Count = 3 };
        meta.AddData("details", expected);

        Assert.Same(expected, meta.GetData<TestMetadata>("details"));
        Assert.True(meta.TryGetData<TestMetadata>("details", out var actual));
        Assert.Same(expected, actual);
    }

    /// <summary>
    /// JObject metadata converts to requested type and caches converted instance.
    /// </summary>
    [Fact]
    public void JObjectDataConvertsToTypedValue()
    {
        var meta = new AssetMeta("asset");
        meta.AddData("details", JObject.Parse("""{"Name":"surface","Count":3}"""));

        var converted = meta.GetData<TestMetadata>("details");

        Assert.NotNull(converted);
        Assert.Equal("surface", converted.Name);
        Assert.Equal(3, converted.Count);
        Assert.Same(converted, meta.GetData<TestMetadata>("details"));
    }

    /// <summary>
    /// Missing key returns default and false, while incompatible stored type throws on typed access.
    /// </summary>
    [Fact]
    public void MissingAndWrongTypeDataFollowTypedContract()
    {
        var meta = new AssetMeta("asset");
        meta.AddData("count", "three");

        Assert.Null(meta.GetData<TestMetadata>("missing"));
        Assert.False(meta.TryGetData<TestMetadata>("missing", out var missing));
        Assert.Null(missing);
        Assert.Throws<InvalidCastException>(() => meta.GetData<int>("count"));
    }

    /// <summary>
    /// Copy replaces asset ID and shares stored metadata object references.
    /// </summary>
    [Fact]
    public void CopyChangesIdAndCopiesDataShallowly()
    {
        var details = new TestMetadata { Name = "before" };
        var original = new AssetMeta("original");
        original.AddData("details", details);

        var copy = original.CreateCopyWithId("copy");
        details.Name = "after";

        Assert.Equal("original", original.AssetId);
        Assert.Equal("copy", copy.AssetId);
        Assert.Same(details, copy.GetData<TestMetadata>("details"));
        Assert.Equal("after", copy.GetData<TestMetadata>("details")!.Name);
    }
}

/// <summary>
/// Verifies behavior metadata key and immutable behavior ID.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Assets")]
public sealed class BehaviourMetaTests
{
    /// <summary>
    /// Behavior metadata exposes stable storage key and constructor ID.
    /// </summary>
    [Fact]
    public void ExposesStableKeyAndBehaviourId()
    {
        var meta = new BehaviourMeta(42);

        Assert.Equal("BehaviourMeta", BehaviourMeta.Key);
        Assert.Equal(42, meta.BehaviourId);
    }
}
