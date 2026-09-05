using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Entities.Sync;

namespace ReiEditor.Tests.Models.Services.Entities.Sync;

/// <summary>
/// Verifies transform extraction and deterministic hierarchy-aware entity ordering.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "EntitySync")]
public sealed class EntitySyncUtilityTests
{
    /// <summary>
    /// Transform extraction accepts JSON integer tokens and ignores preceding non-transform behaviours.
    /// </summary>
    [Fact]
    public void TransformDataReadsJsonIntegerTokens()
    {
        var behaviours = new List<Dictionary<string, object>>
        {
            new() { ["REI_TYPE"] = "Camera", ["_parent"] = 99 },
            new()
            {
                ["REI_TYPE"] = EngineBehavioursConstants.TRANSFORM,
                [EngineBehavioursConstants.TRANSFORM_PARENT] = JToken.FromObject(7),
                [EngineBehavioursConstants.TRANSFORM_ORDER] = JToken.FromObject(3)
            }
        };

        var found = EntitySyncUtility.TryGetTransformData(behaviours, out var parent, out var order);

        Assert.True(found);
        Assert.Equal(7, parent);
        Assert.Equal(3, order);
    }

    /// <summary>
    /// Missing transform data returns false and clears both outputs.
    /// </summary>
    [Fact]
    public void MissingTransformReturnsEmptyOutputs()
    {
        var found = EntitySyncUtility.TryGetTransformData(
            new List<Dictionary<string, object>> { new() { ["REI_TYPE"] = "Camera" } },
            out var parent,
            out var order);

        Assert.False(found);
        Assert.Null(parent);
        Assert.Null(order);
    }

    /// <summary>
    /// Hierarchy ordering emits every parent level before descendants and sorts siblings by order then ID.
    /// </summary>
    [Fact]
    public void OrderedIdsPlaceParentsBeforeChildrenAndSortTiesById()
    {
        var parents = new Dictionary<int, int> { [5] = 2, [4] = 0, [2] = 0, [3] = 2, [7] = 5 };
        var orders = new Dictionary<int, int> { [5] = 1, [4] = 0, [2] = 0, [3] = 1, [7] = 0 };

        var result = EntitySyncUtility.BuildOrderedEntityIds(parents, orders);

        Assert.Equal(new[] { 2, 4, 3, 5, 7 }, result);
    }

    /// <summary>
    /// Missing parents promote entities to roots so no entity ID is lost.
    /// </summary>
    [Fact]
    public void OrderedIdsKeepEntitiesWithMissingParents()
    {
        var parents = new Dictionary<int, int> { [10] = 99, [11] = 10 };
        var orders = new Dictionary<int, int> { [10] = 4, [11] = 0 };

        var result = EntitySyncUtility.BuildOrderedEntityIds(parents, orders);

        Assert.Equal(new[] { 10, 11 }, result);
    }

    /// <summary>
    /// Cycles are appended deterministically after reachable entities without duplicates or omissions.
    /// </summary>
    [Fact]
    public void OrderedIdsKeepCyclesDeterministically()
    {
        var parents = new Dictionary<int, int> { [1] = 0, [6] = 7, [7] = 6 };
        var orders = new Dictionary<int, int> { [1] = 0, [6] = 2, [7] = 1 };

        var result = EntitySyncUtility.BuildOrderedEntityIds(parents, orders);

        Assert.Equal(new[] { 1, 7, 6 }, result);
    }
}
