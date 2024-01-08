using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;

namespace ReiEditor.Models.Services.Scenes;

public class Scene : Asset, IDeserializationCallback
{
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

    public void OnDeserialization(object? sender)
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
        _entities.Remove(entity);
        
        var node = Hierarchy.GetNode(entity);
        if (node == null) return;
        
        foreach (var child in Hierarchy.GetAllChildNodes(node))
        {
            _entities.Remove(child.Content);
        }
            
        Hierarchy.DeleteNode(node);

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

    private void CreateHierarchy()
    {
        Hierarchy = new Hierarchy<GameEntity>(Name);
        
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
                var parent = GetById(e.Transform.Parent);
                if (parent == null) throw new Exception($"Missing parent node for {e}, parent id = {e.Transform.Parent}");
                
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
}