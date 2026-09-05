using ReiEditor.Models.Services.Hierarchies;

namespace ReiEditor.Tests.Models.Services.Hierarchies;

/// <summary>
/// Verifies hierarchy lookup, mutation, ordering, and event contracts.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Hierarchy")]
public sealed class HierarchyTests
{
    /// <summary>
    /// Adding root and child nodes registers both while preserving their existing relationship.
    /// </summary>
    [Fact]
    public void TestAddNodeRegistersRootAndChildAndRaisesEvents()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var root = new HierarchyNode<string>("root", null);
        var child = new HierarchyNode<string>("child", root);
        root.PushChild(child);
        var added = new List<HierarchyNode<string>>();
        hierarchy.NodeAddedEvent += added.Add;

        hierarchy.AddNode(root, isRoot: true);
        hierarchy.AddNode(child, isRoot: false);

        Assert.Same(root, hierarchy.GetNode("root"));
        Assert.Same(child, hierarchy.GetNode("child"));
        Assert.Same(root, Assert.Single(hierarchy.RootNodes));
        Assert.Same(child, Assert.Single(root.ChildNodes));
        Assert.Equal(new[] { root, child }, added);
    }

    /// <summary>
    /// Adding a parent does not implicitly register children because callers register each node explicitly.
    /// </summary>
    [Fact]
    public void TestAddNodeDoesNotRecursivelyRegisterChildren()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var root = new HierarchyNode<string>("root", null);
        root.PushChild(new HierarchyNode<string>("child", root));

        hierarchy.AddNode(root, isRoot: true);

        Assert.Null(hierarchy.GetNode("child"));
    }

    /// <summary>
    /// Rejecting duplicate content leaves root storage, lookup, and notifications unchanged.
    /// </summary>
    [Fact]
    public void TestAddDuplicateRootIsAtomic()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var existing = new HierarchyNode<string>("duplicate", null);
        hierarchy.AddNode(existing, isRoot: true);
        var addedCount = 0;
        hierarchy.NodeAddedEvent += _ => addedCount++;

        Assert.Throws<ArgumentException>(() => hierarchy.AddNode(new HierarchyNode<string>("duplicate", null), isRoot: true));

        Assert.Same(existing, Assert.Single(hierarchy.RootNodes));
        Assert.Same(existing, hierarchy.GetNode("duplicate"));
        Assert.Equal(0, addedCount);
    }

    /// <summary>Re-registering the same root fails without duplication and leaves later valid registrations usable.</summary>
    [Fact]
    public void TestRepeatedRootInstanceRegistrationLeavesHierarchyUsable()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var root = AddRoot(hierarchy, "root");
        var events = new List<HierarchyNode<string>>();
        hierarchy.NodeAddedEvent += events.Add;

        Assert.Throws<ArgumentException>(() => hierarchy.AddNode(root, isRoot: true));
        var next = AddRoot(hierarchy, "next");

        Assert.Equal(new[] { root, next }, hierarchy.RootNodes);
        Assert.Same(root, hierarchy.GetNode("root"));
        Assert.Same(next, hierarchy.GetNode("next"));
        Assert.Same(next, Assert.Single(events));
    }

    /// <summary>Content already registered as a child cannot be inserted again as a root.</summary>
    [Fact]
    public void TestDuplicateChildContentDoesNotCreateAnOrphanRoot()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var parent = AddRoot(hierarchy, "parent");
        var child = AddChild(hierarchy, parent, "child");
        var events = 0;
        hierarchy.NodeAddedEvent += _ => events++;

        Assert.Throws<ArgumentException>(() => hierarchy.AddNode(new HierarchyNode<string>("child", null), isRoot: true));

        Assert.Same(parent, Assert.Single(hierarchy.RootNodes));
        Assert.Same(child, Assert.Single(parent.ChildNodes));
        Assert.Same(parent, child.Parent);
        Assert.Same(child, hierarchy.GetNode("child"));
        Assert.Equal(0, events);
    }

    /// <summary>
    /// Deleting a node removes its full subtree from lookup and emits one event after state changes.
    /// </summary>
    [Fact]
    public void TestDeleteNodeRemovesSubtreeAndRaisesOnePostMutationEvent()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var root = AddRoot(hierarchy, "root");
        var child = AddChild(hierarchy, root, "child");
        AddChild(hierarchy, child, "grandchild");
        var removed = new List<HierarchyNode<string>>();
        hierarchy.NodeRemovedEvent += node =>
        {
            Assert.Null(hierarchy.GetNode("root"));
            Assert.Null(hierarchy.GetNode("child"));
            Assert.Null(hierarchy.GetNode("grandchild"));
            Assert.Empty(hierarchy.RootNodes);
            removed.Add(node);
        };

        hierarchy.DeleteNode(root);

        Assert.Equal(new[] { root }, removed);
    }

    /// <summary>
    /// Moving between parents updates both child lists, parent reference, and final event indices.
    /// </summary>
    [Fact]
    public void TestMoveNodeBetweenParentsUpdatesStructureAndReportsFinalIndices()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var oldParent = AddRoot(hierarchy, "old-parent");
        AddChild(hierarchy, oldParent, "first");
        var movedNode = AddChild(hierarchy, oldParent, "moved");
        var newParent = AddRoot(hierarchy, "new-parent");
        AddChild(hierarchy, newParent, "existing");
        (HierarchyNode<string> Node, HierarchyNode<string>? OldParent, int OldOrder, int NewOrder)? moveEvent = null;
        hierarchy.NodeMovedEvent += (node, parent, oldOrder, newOrder) => moveEvent = (node, parent, oldOrder, newOrder);

        var moved = hierarchy.MoveNode(movedNode, newParent, 0);

        Assert.True(moved);
        Assert.Same(newParent, movedNode.Parent);
        Assert.Equal(new[] { "first" }, oldParent.ChildNodes.Select(x => x.Content));
        Assert.Equal(new[] { "moved", "existing" }, newParent.ChildNodes.Select(x => x.Content));
        Assert.True(moveEvent.HasValue);
        Assert.Same(movedNode, moveEvent.Value.Node);
        Assert.Same(oldParent, moveEvent.Value.OldParent);
        Assert.Equal(1, moveEvent.Value.OldOrder);
        Assert.Equal(0, moveEvent.Value.NewOrder);
    }

    /// <summary>
    /// Moving forward changes final sibling order but reports the pre-removal insertion index consumed by the UI controller.
    /// </summary>
    [Fact]
    public void TestMoveNodeForwardPreservesInsertionIndexForEventConsumers()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var first = AddRoot(hierarchy, "first");
        AddRoot(hierarchy, "second");
        AddRoot(hierarchy, "third");
        var reportedNewOrder = -1;
        hierarchy.NodeMovedEvent += (_, _, _, newOrder) => reportedNewOrder = newOrder;

        var moved = hierarchy.MoveNode(first, null, 3);

        Assert.True(moved);
        Assert.Equal(new[] { "second", "third", "first" }, hierarchy.RootNodes.Select(x => x.Content));
        Assert.Equal(2, hierarchy.GetNodeOrder(first));
        // HierarchyNodeCollectionController applies the removal offset when replaying this event.
        Assert.Equal(3, reportedNewOrder);
    }

    /// <summary>
    /// Oversized same-parent indices clamp to the final valid end position.
    /// </summary>
    [Fact]
    public void TestMoveNodeWithOversizedSameParentIndexClampsToEnd()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var first = AddRoot(hierarchy, "first");
        AddRoot(hierarchy, "second");

        var exception = Record.Exception(() => hierarchy.MoveNode(first, null, int.MaxValue));

        Assert.Null(exception);
        Assert.Equal(new[] { "second", "first" }, hierarchy.RootNodes.Select(x => x.Content));
    }

    /// <summary>Same-parent root and child moves clamp before removal while preserving raw insertion indices in events.</summary>
    [Theory]
    [InlineData(false, 0, int.MaxValue, "b,c,a")]
    [InlineData(true, 0, int.MaxValue, "b,c,a")]
    [InlineData(false, 2, int.MinValue, "c,a,b")]
    [InlineData(true, 2, int.MinValue, "c,a,b")]
    [InlineData(false, 1, 99, "a,c,b")]
    [InlineData(true, 1, 99, "a,c,b")]
    [InlineData(false, 0, 3, "b,c,a")]
    [InlineData(true, 0, 3, "b,c,a")]
    public void TestSameParentMoveClampsBeforeRemoval(bool nested, int sourceIndex, int insertionIndex, string expectedOrder)
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var parent = nested ? AddRoot(hierarchy, "parent") : null;
        var nodes = new[] { "a", "b", "c" }.Select(name => parent == null ? AddRoot(hierarchy, name) : AddChild(hierarchy, parent, name)).ToArray();
        var movedNode = nodes[sourceIndex];
        var events = new List<(HierarchyNode<string> Node, HierarchyNode<string>? Parent, int Old, int New)>();
        hierarchy.NodeMovedEvent += (node, oldParent, oldOrder, newOrder) => events.Add((node, oldParent, oldOrder, newOrder));

        Assert.True(hierarchy.MoveNode(movedNode, parent, insertionIndex));

        var siblings = (parent?.ChildNodes ?? hierarchy.RootNodes).ToArray();
        Assert.Equal(expectedOrder, string.Join(',', siblings.Select(node => node.Content)));
        Assert.Equal(3, siblings.Distinct().Count());
        Assert.All(nodes, node => Assert.Same(node, hierarchy.GetNode(node.Content)));
        Assert.Same(parent, movedNode.Parent);
        Assert.Equal((movedNode, parent, sourceIndex, insertionIndex), Assert.Single(events));
    }

    /// <summary>Root/child transfers report the actual source index and preserve all nodes when destination indices are extreme.</summary>
    [Theory]
    [InlineData(false, int.MaxValue)]
    [InlineData(false, int.MinValue)]
    [InlineData(true, int.MaxValue)]
    [InlineData(true, int.MinValue)]
    public void TestCrossParentMoveRetainsSourceIndexAndClampsDestination(bool toRoot, int insertionIndex)
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var parent = AddRoot(hierarchy, "parent");
        var rootSibling = AddRoot(hierarchy, "root-sibling");
        var childSibling = AddChild(hierarchy, parent, "child-sibling");
        var moved = toRoot ? AddChild(hierarchy, parent, "moved") : AddRoot(hierarchy, "moved");
        var sourceParent = moved.Parent;
        var sourceIndex = toRoot ? 1 : 2;
        var destinationParent = toRoot ? null : parent;
        var events = new List<(HierarchyNode<string>? Parent, int Old, int New)>();
        hierarchy.NodeMovedEvent += (_, oldParent, oldOrder, newOrder) => events.Add((oldParent, oldOrder, newOrder));

        Assert.True(hierarchy.MoveNode(moved, destinationParent, insertionIndex));

        Assert.Equal((sourceParent, sourceIndex, insertionIndex), Assert.Single(events));
        Assert.Same(destinationParent, moved.Parent);
        var destination = (destinationParent?.ChildNodes ?? hierarchy.RootNodes).ToArray();
        Assert.Same(moved, insertionIndex < 0 ? destination.First() : destination.Last());
        Assert.Equal(toRoot ? new[] { childSibling } : new[] { parent, rootSibling }, toRoot ? parent.ChildNodes : hierarchy.RootNodes);
        Assert.All(new[] { parent, rootSibling, childSibling, moved }, node => Assert.Same(node, hierarchy.GetNode(node.Content)));
    }

    /// <summary>
    /// Negative indices clamp to first position in destination parent.
    /// </summary>
    [Fact]
    public void TestMoveNodeWithNegativeIndexClampsToStart()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var node = AddRoot(hierarchy, "node");
        var parent = AddRoot(hierarchy, "parent");
        AddChild(hierarchy, parent, "existing");

        var moved = hierarchy.MoveNode(node, parent, -10);

        Assert.True(moved);
        Assert.Equal(new[] { "node", "existing" }, parent.ChildNodes.Select(x => x.Content));
        Assert.Same(parent, node.Parent);
    }

    /// <summary>
    /// Moving a child to root and back preserves lookup while updating membership and parent state.
    /// </summary>
    [Fact]
    public void TestMoveNodeBetweenChildAndRootPreservesLookup()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var parent = AddRoot(hierarchy, "parent");
        var child = AddChild(hierarchy, parent, "child");

        Assert.True(hierarchy.MoveNode(child, null, 0));
        Assert.Null(child.Parent);
        Assert.Equal(new[] { "child", "parent" }, hierarchy.RootNodes.Select(x => x.Content));

        Assert.True(hierarchy.MoveNode(child, parent, 0));
        Assert.Same(parent, child.Parent);
        Assert.Same(child, Assert.Single(parent.ChildNodes));
        Assert.Same(child, hierarchy.GetNode("child"));
    }

    /// <summary>
    /// Moving a node to its current index is a no-op without notification.
    /// </summary>
    [Fact]
    public void TestMoveNodeToSameIndexReturnsFalseWithoutEvent()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var node = AddRoot(hierarchy, "node");
        var moveEvents = 0;
        hierarchy.NodeMovedEvent += (_, _, _, _) => moveEvents++;

        var moved = hierarchy.MoveNode(node, null, 0);

        Assert.False(moved);
        Assert.Equal(0, moveEvents);
    }

    /// <summary>
    /// Moving a node under itself leaves hierarchy unchanged and emits no event.
    /// </summary>
    [Fact]
    public void TestMoveNodeToSelfRejectsCycleWithoutChangingHierarchy()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var root = AddRoot(hierarchy, "root");
        var moveEvents = 0;
        hierarchy.NodeMovedEvent += (_, _, _, _) => moveEvents++;

        var moved = hierarchy.MoveNode(root, root, 0);

        Assert.False(moved);
        Assert.Null(root.Parent);
        Assert.Same(root, Assert.Single(hierarchy.RootNodes));
        Assert.Equal(0, moveEvents);
    }

    /// <summary>
    /// Moving a node under a descendant leaves tree and lookup map unchanged and emits no event.
    /// </summary>
    [Fact]
    public void TestMoveNodeToDescendantRejectsCycleWithoutChangingHierarchy()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var root = AddRoot(hierarchy, "root");
        var child = AddChild(hierarchy, root, "child");
        var moveEvents = 0;
        hierarchy.NodeMovedEvent += (_, _, _, _) => moveEvents++;

        var moved = hierarchy.MoveNode(root, child, 0);

        Assert.False(moved);
        Assert.Null(root.Parent);
        Assert.Same(root, child.Parent);
        Assert.Same(root, Assert.Single(hierarchy.RootNodes));
        Assert.Same(child, Assert.Single(root.ChildNodes));
        Assert.Empty(child.ChildNodes);
        Assert.Same(root, hierarchy.GetNode("root"));
        Assert.Same(child, hierarchy.GetNode("child"));
        Assert.Equal(0, moveEvents);
    }

    /// <summary>
    /// Adds and returns a registered root node.
    /// </summary>
    private static HierarchyNode<string> AddRoot(Hierarchy<string> hierarchy, string content)
    {
        var node = new HierarchyNode<string>(content, null);
        hierarchy.AddNode(node, isRoot: true);
        return node;
    }

    /// <summary>
    /// Adds and returns a registered child node.
    /// </summary>
    private static HierarchyNode<string> AddChild(Hierarchy<string> hierarchy, HierarchyNode<string> parent, string content)
    {
        var node = new HierarchyNode<string>(content, parent);
        parent.PushChild(node);
        hierarchy.AddNode(node, isRoot: false);
        return node;
    }
}
