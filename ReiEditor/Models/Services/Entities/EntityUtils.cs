using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Entities;

public static class EntityUtils
{
    public static List<GameEntity> GetEntitiesForRecursiveDestroy(Scene scene, GameEntity root)
    {
        var node = scene.Hierarchy.GetNode(root);
        if (node == null) return new List<GameEntity> { root };

        var childNodes = scene.Hierarchy.GetAllChildNodes(node).ToList();
        var depths = new Dictionary<HierarchyNode<GameEntity>, int>();

        foreach (var child in childNodes)
        {
            depths[child] = GetDepth(child);
        }

        childNodes.Sort((left, right) =>
        {
            var depthCompare = depths[right].CompareTo(depths[left]);
            return depthCompare != 0 ? depthCompare : left.Content.Transform.Order.CompareTo(right.Content.Transform.Order);
        });

        var entities = new List<GameEntity>(childNodes.Count + 1);
        entities.AddRange(childNodes.Select(child => child.Content));

        entities.Add(root);
        return entities;
    }

    public static int GetDepth(HierarchyNode<GameEntity> node)
    {
        var depth = 0;
        var current = node.Parent;
        while (current != null)
        {
            depth++;
            current = current.Parent;
        }

        return depth;
    }
}