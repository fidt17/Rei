using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Tests.Models.Services.Components;

/// <summary>
/// Verifies property mutation validation and child subscription changes for real property trees.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Components")]
public sealed class SerializedPropertyTests
{
    /// <summary>
    /// A changed scalar is stored before one notification and assigning the same value again stays quiet.
    /// </summary>
    [Fact]
    public void ScalarChangePublishesNewValueOnce()
    {
        var property = Integer("count", 1);
        var changes = new List<object?>();
        property.ValueChangedEvent += value =>
        {
            Assert.Equal(value, property.Value);
            changes.Add(value);
        };

        property.Value = 2;
        property.Value = 2;

        Assert.Equal(2, Assert.IsType<int>(property.Value));
        Assert.Equal(2, Assert.IsType<int>(Assert.Single(changes)));
    }

    /// <summary>
    /// Invalid numeric values and primitive null assignments preserve the old scalar without notifications or exceptions.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("2")]
    [InlineData(2.5)]
    public void InvalidScalarAssignmentPreservesOldValue(object? value)
    {
        var property = Integer("count", 7);
        var notifications = 0;
        property.ValueChangedEvent += _ => notifications++;

        var error = Record.Exception(() => property.Value = value);

        Assert.Null(error);
        Assert.Equal(7, Assert.IsType<int>(property.Value));
        Assert.Equal(0, notifications);
    }

    /// <summary>
    /// Silent scalar mutation applies validation and publishes no event until explicitly triggered.
    /// </summary>
    [Fact]
    public void SilentScalarChangeWaitsForExplicitNotification()
    {
        var property = Integer("count", 1);
        var changes = new List<object?>();
        property.ValueChangedEvent += changes.Add;

        property.SetValueWithoutTriggeringChangedEvent(4);
        property.SetValueWithoutTriggeringChangedEvent("invalid");

        Assert.Equal(4, Assert.IsType<int>(property.Value));
        Assert.Empty(changes);

        property.TriggerChangedEvent();

        Assert.Equal(4, Assert.IsType<int>(Assert.Single(changes)));
    }

    /// <summary>
    /// Replacing either supported child container detaches old children and forwards the new container when new children change.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacingChildContainerRefreshesSubscriptions(bool collection)
    {
        var oldChild = Integer("old", 1);
        var newChild = Integer("new", 2);
        var oldContainer = Container(collection, oldChild);
        var newContainer = Container(collection, newChild);
        var parent = new SerializedProperty("root", collection ? SerializedTypeEnum.Collection : SerializedTypeEnum.Custom,
            oldContainer, collection ? "vector<int>" : "Object", null);
        var changes = new List<object?>();
        parent.ValueChangedEvent += changes.Add;

        oldChild.Value = 3;
        Assert.Same(oldContainer, Assert.Single(changes));
        changes.Clear();

        parent.Value = newContainer;
        Assert.Same(newContainer, Assert.Single(changes));
        changes.Clear();
        oldChild.Value = 4;
        Assert.Empty(changes);
        newChild.Value = 5;

        Assert.Same(newContainer, Assert.Single(changes));
    }

    /// <summary>
    /// Clearing a custom value detaches former children so they cannot emit stale parent events.
    /// </summary>
    [Fact]
    public void ClearingCustomValueDetachesOldChildren()
    {
        var child = Integer("x", 1);
        var parent = new SerializedProperty("root", SerializedTypeEnum.Custom, Container(false, child), "Object", null);
        var changes = new List<object?>();
        parent.ValueChangedEvent += changes.Add;

        parent.Value = null;
        Assert.Null(parent.Value);
        Assert.Null(Assert.Single(changes));
        changes.Clear();
        child.Value = 2;

        Assert.Empty(changes);
    }

    /// <summary>
    /// Structural refresh removes deleted child subscriptions, adds inserted children, and avoids duplicate subscriptions.
    /// </summary>
    [Fact]
    public void NotifyStructureChangedRefreshesMutatedListWithoutDuplicateHandlers()
    {
        var removed = Integer("removed", 1);
        var retained = Integer("retained", 2);
        var added = Integer("added", 3);
        var children = new List<SerializedProperty> { removed, retained };
        var parent = new SerializedProperty("items", SerializedTypeEnum.Collection, children, "vector<int>", null);
        var changes = new List<object?>();
        parent.ValueChangedEvent += changes.Add;
        children.Remove(removed);
        children.Add(added);

        parent.NotifyStructureChanged();
        parent.NotifyStructureChanged();
        Assert.Equal(2, changes.Count);
        Assert.All(changes, value => Assert.Same(children, value));
        changes.Clear();
        removed.Value = 10;
        Assert.Empty(changes);
        retained.Value = 20;
        added.Value = 30;

        Assert.Equal(2, changes.Count);
        Assert.All(changes, value => Assert.Same(children, value));
    }

    /// <summary>
    /// Raw dictionaries update known child properties without replacing the typed tree or adding unknown keys.
    /// </summary>
    [Fact]
    public void RawDictionaryUpdatePreservesTreeAndUnknownKeys()
    {
        var child = Integer("x", 1);
        var children = new Dictionary<string, SerializedProperty> { ["x"] = child };
        var parent = new SerializedProperty("root", SerializedTypeEnum.Custom, children, "Object", null);
        var changes = new List<object?>();
        parent.ValueChangedEvent += changes.Add;

        parent.Value = new Dictionary<string, object?> { ["x"] = 9, ["unknown"] = 100 };

        Assert.Same(children, parent.Value);
        Assert.Same(child, Assert.Single(children).Value);
        Assert.Equal(9, Assert.IsType<int>(child.Value));
        Assert.Same(children, Assert.Single(changes));
    }

    /// <summary>
    /// Raw lists update the overlapping prefix while preserving property identities and the original list length.
    /// </summary>
    [Theory]
    [InlineData(1, 20)]
    [InlineData(3, 2)]
    public void RawListUpdateDoesNotResizeExistingTree(int inputCount, int expectedSecond)
    {
        var first = Integer("0", 10);
        var second = Integer("1", 20);
        var children = new List<SerializedProperty> { first, second };
        var parent = new SerializedProperty("items", SerializedTypeEnum.Collection, children, "vector<int>", null);
        var values = Enumerable.Range(1, inputCount).Select(value => (object?)value).ToList();

        parent.Value = values;

        Assert.Same(children, parent.Value);
        Assert.Equal(2, children.Count);
        Assert.Same(first, children[0]);
        Assert.Same(second, children[1]);
        Assert.Equal(1, Assert.IsType<int>(first.Value));
        Assert.Equal(expectedSecond, Assert.IsType<int>(second.Value));
    }

    /// <summary>
    /// Nested custom-to-collection children propagate one root event, and hierarchy output orders ancestors before descendants.
    /// </summary>
    [Fact]
    public void NestedChildChangePropagatesAndHierarchyRunsRootToLeaf()
    {
        var root = new SerializedProperty("root", SerializedTypeEnum.Custom, null, "Object", null);
        var list = new SerializedProperty("items", SerializedTypeEnum.Collection, null, "vector<int>", root);
        var leaf = new SerializedProperty("0", SerializedTypeEnum.Integer, 1, "int", list);
        list.Value = new List<SerializedProperty> { leaf };
        root.Value = new Dictionary<string, SerializedProperty> { ["items"] = list };
        var changes = new List<object?>();
        root.ValueChangedEvent += changes.Add;
        var hierarchy = new List<SerializedProperty>();

        leaf.Value = 2;
        leaf.FillPropertyHierarchy(hierarchy);

        Assert.Same(root.Value, Assert.Single(changes));
        Assert.Equal(new[] { root, list, leaf }, hierarchy);
    }

    private static SerializedProperty Integer(string name, int value) => new(name, SerializedTypeEnum.Integer, value, "int", null);

    private static object Container(bool collection, SerializedProperty child)
        => collection ? new List<SerializedProperty> { child } : new Dictionary<string, SerializedProperty> { [child.Name] = child };
}
