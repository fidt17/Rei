using ReiEditor.Models.Services.Hierarchies;

namespace ReiEditor.Tests.Models.Services.Hierarchies;

/// <summary>
/// Verifies hierarchy invariants when nodes are moved between parents.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Hierarchy")]
public sealed class HierarchyTests
{
    /// <summary>
    /// Moving a node under its descendant must leave the tree and lookup map unchanged and emit no move event.
    /// </summary>
    [Fact]
    public void MoveNodeToDescendantRejectsCycleWithoutChangingHierarchy()
    {
        var hierarchy = new Hierarchy<string>("Scene");
        var root = new HierarchyNode<string>("root", null);
        var child = new HierarchyNode<string>("child", root);
        root.PushChild(child);
        hierarchy.AddNode(root, isRoot: true);
        hierarchy.AddNode(child, isRoot: false);
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
}
