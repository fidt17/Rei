using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Tests.Models.Services.Scenes;

/// <summary>
/// Verifies scene entity storage, hierarchy rebuilding, ordering, and transform synchronization.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Scenes")]
public sealed class SceneTests
{
    /// <summary>
    /// Entity IDs begin at one and advance beyond maximum existing ID.
    /// </summary>
    [Fact]
    public void TestAllocateEntityIdUsesOneThenMaximumPlusOne()
    {
        var scene = new Scene("Scene");

        Assert.Equal(1, scene.AllocateEntityId());

        scene.AddEntity(new GameEntity(7, "Seven"));
        scene.AddEntity(new GameEntity(2, "Two"));

        Assert.Equal(8, scene.AllocateEntityId());
    }

    /// <summary>
    /// Adding entity registers root lookup and resets persisted transform to root order.
    /// </summary>
    [Fact]
    public void TestAddEntityRegistersRootAndRefreshesTransform()
    {
        var scene = new Scene("Scene");
        var existing = new GameEntity(1, "Existing");
        scene.AddEntity(existing);
        var entity = new GameEntity(2, "Added");
        entity.Transform.SetParent(99);
        entity.Transform.SetOrder(50);

        scene.AddEntity(entity);

        Assert.Equal(new[] { existing, entity }, scene.Entities);
        Assert.Same(entity, scene.GetById(2));
        Assert.Same(entity, scene.Hierarchy.GetNode(entity)?.Content);
        Assert.Null(scene.Hierarchy.GetNode(entity)?.Parent);
        Assert.Equal(0, entity.Transform.Parent);
        Assert.Equal(1, entity.Transform.Order);
    }

    /// <summary>
    /// Adding duplicate entity ID throws without changing entity or hierarchy collections.
    /// </summary>
    [Fact]
    public void TestAddDuplicateEntityIdThrowsWithoutMutation()
    {
        var scene = new Scene("Scene");
        var existing = new GameEntity(1, "Existing");
        scene.AddEntity(existing);

        Assert.Throws<Exception>(() => scene.AddEntity(new GameEntity(1, "Duplicate")));

        Assert.Same(existing, Assert.Single(scene.Entities));
        Assert.Same(existing, Assert.Single(scene.Hierarchy.RootNodes).Content);
    }

    /// <summary>
    /// Moving entity between root and parent updates hierarchy plus parent and sibling orders.
    /// </summary>
    [Fact]
    public void TestMoveEntityUpdatesHierarchyAndTransformParentAndOrders()
    {
        var scene = new Scene("Scene");
        var parent = AddEntity(scene, 1, "Parent");
        var sibling = AddEntity(scene, 2, "Sibling");
        var moved = AddEntity(scene, 3, "Moved");

        Assert.True(scene.MoveEntity(moved, parent, 0));
        Assert.Same(parent, scene.Hierarchy.GetNode(moved)?.Parent?.Content);
        Assert.Equal(1, moved.Transform.Parent);
        Assert.Equal(0, moved.Transform.Order);
        Assert.Equal(0, parent.Transform.Order);
        Assert.Equal(1, sibling.Transform.Order);

        Assert.True(scene.MoveEntity(moved, null, 1));
        Assert.Null(scene.Hierarchy.GetNode(moved)?.Parent);
        Assert.Equal(0, moved.Transform.Parent);
        Assert.Equal(new[] { parent, moved, sibling }, scene.Hierarchy.RootNodes.Select(x => x.Content));
        Assert.Equal(new[] { 0, 1, 2 }, scene.Hierarchy.RootNodes.Select(x => x.Content.Transform.Order));
    }

    /// <summary>
    /// Missing entity cannot move and leaves scene unchanged.
    /// </summary>
    [Fact]
    public void TestMoveMissingEntityReturnsFalseWithoutMutation()
    {
        var scene = new Scene("Scene");
        var existing = AddEntity(scene, 1, "Existing");

        var moved = scene.MoveEntity(new GameEntity(2, "Missing"), existing, 0);

        Assert.False(moved);
        Assert.Same(existing, Assert.Single(scene.Entities));
        Assert.Same(existing, Assert.Single(scene.Hierarchy.RootNodes).Content);
    }

    /// <summary>
    /// Deleting parent removes full subtree from entities and lookup then compacts remaining root orders.
    /// </summary>
    [Fact]
    public void TestDeleteEntityRemovesSubtreeAndCompactsRemainingOrders()
    {
        var scene = new Scene("Scene");
        var parent = AddEntity(scene, 1, "Parent");
        var child = AddEntity(scene, 2, "Child");
        var grandchild = AddEntity(scene, 3, "Grandchild");
        var survivor = AddEntity(scene, 4, "Survivor");
        scene.MoveEntity(child, parent, 0);
        scene.MoveEntity(grandchild, child, 0);

        scene.DeleteEntity(parent);

        Assert.Equal(new[] { survivor }, scene.Entities);
        Assert.Null(scene.GetById(1));
        Assert.Null(scene.GetById(2));
        Assert.Null(scene.GetById(3));
        Assert.Null(scene.Hierarchy.GetNode(parent));
        Assert.Null(scene.Hierarchy.GetNode(child));
        Assert.Null(scene.Hierarchy.GetNode(grandchild));
        Assert.Equal(0, survivor.Transform.Order);
    }

    /// <summary>
    /// Rebuilding shuffled persisted entities restores parent links and sibling order.
    /// </summary>
    [Fact]
    public void TestRebuildHierarchyRestoresShuffledPersistedTree()
    {
        var scene = new Scene("Persisted");
        var secondChild = AddEntity(scene, 3, "Second child");
        var parent = AddEntity(scene, 1, "Parent");
        var firstChild = AddEntity(scene, 2, "First child");
        var otherRoot = AddEntity(scene, 4, "Other root");
        SetTransform(secondChild, parentId: 1, order: 8);
        SetTransform(parent, parentId: 0, order: 5);
        SetTransform(firstChild, parentId: 1, order: 2);
        SetTransform(otherRoot, parentId: 0, order: 1);

        scene.RebuildHierarchy();

        Assert.Equal(new[] { otherRoot, parent }, scene.Hierarchy.RootNodes.Select(x => x.Content));
        var parentNode = scene.Hierarchy.GetNode(parent);
        Assert.NotNull(parentNode);
        Assert.Equal(new[] { firstChild, secondChild }, parentNode.ChildNodes.Select(x => x.Content));
        Assert.Same(parentNode, scene.Hierarchy.GetNode(firstChild)?.Parent);
        Assert.Same(parentNode, scene.Hierarchy.GetNode(secondChild)?.Parent);
    }

    /// <summary>
    /// Rebuild repairs missing and self parents and breaks cycles while retaining every entity.
    /// </summary>
    [Fact]
    public void TestRebuildHierarchyRepairsInvalidParentLinksAndBreaksCycles()
    {
        var scene = new Scene("Invalid");
        var missingParent = AddEntity(scene, 1, "Missing parent");
        var selfParent = AddEntity(scene, 2, "Self parent");
        var cycleStart = AddEntity(scene, 3, "Cycle start");
        var cycleEnd = AddEntity(scene, 4, "Cycle end");
        SetTransform(missingParent, parentId: 99, order: 0);
        SetTransform(selfParent, parentId: 2, order: 0);
        SetTransform(cycleStart, parentId: 4, order: 0);
        SetTransform(cycleEnd, parentId: 3, order: 0);

        scene.RebuildHierarchy();

        Assert.Equal(0, missingParent.Transform.Parent);
        Assert.Equal(0, selfParent.Transform.Parent);
        Assert.Equal(0, cycleStart.Transform.Parent);
        Assert.Equal(3, cycleEnd.Transform.Parent);
        Assert.Equal(4, scene.Entities.Count());
        Assert.All(scene.Entities, entity => Assert.NotNull(scene.Hierarchy.GetNode(entity)));
        Assert.Null(scene.Hierarchy.GetNode(missingParent)?.Parent);
        Assert.Null(scene.Hierarchy.GetNode(selfParent)?.Parent);
        Assert.Null(scene.Hierarchy.GetNode(cycleStart)?.Parent);
        Assert.Same(cycleStart, scene.Hierarchy.GetNode(cycleEnd)?.Parent?.Content);
    }

    /// <summary>
    /// Rebuild event runs after complete hierarchy replacement and invalid-link repair.
    /// </summary>
    [Fact]
    public void TestRebuildHierarchyRaisesEventAfterStateIsReady()
    {
        var scene = new Scene("Scene");
        var entity = AddEntity(scene, 1, "Entity");
        entity.Transform.SetParent(99);
        var eventCount = 0;
        scene.HierarchyRebuiltEvent += () =>
        {
            eventCount++;
            Assert.Equal(0, entity.Transform.Parent);
            Assert.Same(entity, Assert.Single(scene.Hierarchy.RootNodes).Content);
            Assert.Same(entity, scene.Hierarchy.GetNode(entity)?.Content);
        };

        scene.RebuildHierarchy();

        Assert.Equal(1, eventCount);
    }

    /// <summary>
    /// Normalization sorts equal-order roots and children by ID and writes contiguous transform orders.
    /// </summary>
    [Fact]
    public void TestNormalizeTransformOrdersUsesIdTieBreakAndWritesContiguousOrders()
    {
        var scene = new Scene("Scene");
        var highRoot = AddEntity(scene, 20, "High root");
        var highChild = AddEntity(scene, 12, "High child");
        var lowRoot = AddEntity(scene, 10, "Low root");
        var lowChild = AddEntity(scene, 11, "Low child");
        SetTransform(highRoot, parentId: 0, order: 5);
        SetTransform(lowRoot, parentId: 0, order: 5);
        SetTransform(highChild, parentId: 10, order: 7);
        SetTransform(lowChild, parentId: 10, order: 7);
        scene.RebuildHierarchy();

        scene.NormalizeTransformOrders();

        Assert.Equal(new[] { lowRoot, highRoot }, scene.Hierarchy.RootNodes.Select(x => x.Content));
        Assert.Equal(new[] { 0, 1 }, scene.Hierarchy.RootNodes.Select(x => x.Content.Transform.Order));
        var lowRootNode = scene.Hierarchy.GetNode(lowRoot);
        Assert.NotNull(lowRootNode);
        Assert.Equal(new[] { lowChild, highChild }, lowRootNode.ChildNodes.Select(x => x.Content));
        Assert.Equal(new[] { 0, 1 }, lowRootNode.ChildNodes.Select(x => x.Content.Transform.Order));
        Assert.All(lowRootNode.ChildNodes, child => Assert.Equal(lowRoot.Id, child.Content.Transform.Parent));
    }

    /// <summary>
    /// Adds entity to scene and returns it.
    /// </summary>
    private static GameEntity AddEntity(Scene scene, int id, string name)
    {
        var entity = new GameEntity(id, name);
        scene.AddEntity(entity);
        return entity;
    }

    /// <summary>
    /// Applies persisted parent and order values before hierarchy rebuild.
    /// </summary>
    private static void SetTransform(GameEntity entity, int parentId, int order)
    {
        entity.Transform.SetParent(parentId);
        entity.Transform.SetOrder(order);
    }
}
