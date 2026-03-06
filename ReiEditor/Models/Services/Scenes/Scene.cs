using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Scenes;

public class Scene : Asset, IHierarchyProvider<GameEntity>, IOnDeserialized
{
    public event Action? HierarchyRebuiltEvent;
    
    [JsonProperty("Name")]
    public string Name { get; }

    [JsonIgnore]
    public IEnumerable<GameEntity> Entities => _entities;

    [JsonIgnore]
    public Hierarchy<GameEntity> Hierarchy { get; private set; } = new("");
    
    [JsonProperty("Entities")]
    private List<GameEntity> _entities { get; } = new();

    public Scene(string name)
    {
        Name = name;
    }

    public void OnDeserialized()
    {
        CreateHierarchy();
    }

    public void RebuildHierarchy()
    {
        CreateHierarchy();
    }

    public int AllocateEntityId() => _entities.Count == 0 ? 1 : _entities.Max(x => x.Id) + 1;

    public GameEntity? GetById(int id) => _entities.Find(x => x.Id == id);

    public void AddEntity(GameEntity entity)
    {
        if (_entities.Exists(x => x.Equals(entity))) throw new Exception($"Entity with Id {entity.Id} already exists in scene");

        _entities.Add(entity);
        Hierarchy.AddNode(new HierarchyNode<GameEntity>(entity, null), true);
        RefreshEntityTransform(entity);
    }

    public void DeleteEntity(GameEntity entity)
    {
        DeleteEntityInternal(entity, refreshTransforms: true);
    }

    public void DeleteEntity(GameEntity entity, bool refreshTransforms)
    {
        DeleteEntityInternal(entity, refreshTransforms);
    }

    private void DeleteEntityInternal(GameEntity entity, bool refreshTransforms)
    {
        _entities.Remove(entity);
        
        var node = Hierarchy.GetNode(entity);
        if (node == null) return;
        
        foreach (var child in Hierarchy.GetAllChildNodes(node))
        {
            _entities.Remove(child.Content);
        }
            
        Hierarchy.DeleteNode(node);

        if (!refreshTransforms) return;

        if (node.Parent != null)
        {
            RefreshTransforms(node.Parent.ChildNodes.Select(x => x.Content));
        }
        else
        {
            RefreshTransforms(_entities);
        }
    }

    public bool MoveEntity(GameEntity entity, GameEntity? newParent, int idx)
    {
        var node = Hierarchy.GetNode(entity);
        if (node == null) return false;

        var oldParentNode = node.Parent;
        var newParentNode = newParent == null ? null : Hierarchy.GetNode(newParent);
        var didMove = Hierarchy.MoveNode(node, newParentNode, idx);
        if (!didMove) return false;

        if (oldParentNode != null)
        {
            RefreshTransforms(oldParentNode.ChildNodes.Select(x => x.Content));
        }
        else
        {
            RefreshTransforms(_entities.Where(x => !x.Transform.HasParent()));
        }

        if (newParentNode != oldParentNode && newParentNode != null)
        {
            RefreshTransforms(newParentNode.ChildNodes.Select(x => x.Content));
        }
        else if (newParentNode == null)
        {
            entity.Transform.SetParent(0);
            RefreshTransforms(_entities.Where(x => !x.Transform.HasParent()));
        }
        
        return didMove;
    }

    public void NormalizeTransformOrders()
    {
        foreach (var root in Hierarchy.RootNodes)
        {
            SortHierarchyChildren(root, CompareHierarchyNodes);
        }

        Hierarchy.SortRootNodes(CompareHierarchyNodes);

        var rootOrder = 0;
        foreach (var root in Hierarchy.RootNodes)
        {
            root.Content.Transform.SetParent(0);
            root.Content.Transform.SetOrder(rootOrder);
            rootOrder++;

            NormalizeChildOrders(root);
        }
    }

    private void CreateHierarchy()
    {
        Hierarchy = new Hierarchy<GameEntity>(Name);
        var entityById = _entities.ToDictionary(x => x.Id);

        foreach (var entity in _entities)
        {
            if (!entity.Transform.HasParent()) continue;
            if (!HasInvalidParentLink(entity, entityById)) continue;

            entity.Transform.SetParent(0);
        }
        
        foreach (var e in _entities)
        {
            var node = new HierarchyNode<GameEntity>(e, null);
            Hierarchy.AddNode(node, !e.Transform.HasParent());
        }
        
        foreach (var e in _entities)
        {
            var node = Hierarchy.GetNode(e);
            if (node == null) throw new Exception($"Missing node for {e}");

            if (e.Transform.HasParent())
            {
                if (!entityById.TryGetValue(e.Transform.Parent, out var parent))
                {
                    e.Transform.SetParent(0);
                    continue;
                }
                
                var parentNode = Hierarchy.GetNode(parent);
                if (parentNode == null) throw new Exception($"Missing parent node for {parent}");
                
                node.SetParent(parentNode);
                parentNode.PushChild(node);
            }
        }
        
        foreach (var e in _entities)
        {
            var node = Hierarchy.GetNode(e);
            if (node == null) throw new Exception($"Missing node for {e}");
            
            node.SortChildren((a, b) => a.Content.Transform.Order.CompareTo(b.Content.Transform.Order));
        }
        
        Hierarchy.SortRootNodes((a, b) => a.Content.Transform.Order.CompareTo(b.Content.Transform.Order));
        HierarchyRebuiltEvent?.Invoke();
    }

    private void RefreshTransforms(IEnumerable<GameEntity> entities)
    {
        foreach (var e in entities)
        {
            RefreshEntityTransform(e);
        }
    }

    private void RefreshEntityTransform(GameEntity e)
    {
        var node = Hierarchy.GetNode(e);
        if (node == null) throw new Exception($"Missing node for {e}");
        e.Transform.SetOrder(Hierarchy.GetNodeOrder(node));
        e.Transform.SetParent(node.Parent == null ? 0 : node.Parent.Content.Id);
    }

    private void SortHierarchyChildren(HierarchyNode<GameEntity> node, Func<HierarchyNode<GameEntity>, HierarchyNode<GameEntity>, int> comparison)
    {
        node.SortChildren(comparison);

        foreach (var child in node.ChildNodes)
        {
            SortHierarchyChildren(child, comparison);
        }
    }

    private void NormalizeChildOrders(HierarchyNode<GameEntity> parent)
    {
        var order = 0;
        foreach (var child in parent.ChildNodes)
        {
            child.Content.Transform.SetParent(parent.Content.Id);
            child.Content.Transform.SetOrder(order);
            order++;

            NormalizeChildOrders(child);
        }
    }

    private static int CompareHierarchyNodes(HierarchyNode<GameEntity> left, HierarchyNode<GameEntity> right)
    {
        var orderCompare = left.Content.Transform.Order.CompareTo(right.Content.Transform.Order);
        if (orderCompare != 0) return orderCompare;

        return left.Content.Id.CompareTo(right.Content.Id);
    }

    private static bool HasInvalidParentLink(GameEntity entity, IReadOnlyDictionary<int, GameEntity> entityById)
    {
        var parentId = entity.Transform.Parent;
        if (parentId == 0) return false;
        if (parentId == entity.Id) return true;
        if (!entityById.TryGetValue(parentId, out var parent)) return true;

        var visitedEntityIds = new HashSet<int> { entity.Id };
        var current = parent;

        while (current.Transform.HasParent())
        {
            if (!visitedEntityIds.Add(current.Id)) return true;

            var currentParentId = current.Transform.Parent;
            if (currentParentId == current.Id) return true;
            if (!entityById.TryGetValue(currentParentId, out current)) return true;
        }

        return false;
    }
}
