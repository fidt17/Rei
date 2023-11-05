using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Hierarchies;

public class Hierarchy
{
    public class Node
    {
        public Node? Parent { get; }
        public GameEntity Entity { get; }

        public IEnumerable<Node> ChildNodes => _childNodes;

        private readonly List<Node> _childNodes = new();

        public Node(GameEntity entity, Node? parent)
        {
            Entity = entity;
            Parent = parent;
        }

        public void AddNode(Node node) => _childNodes.Add(node);
        public void RemoveNode(Node node) => _childNodes.Remove(node);

        public void SortNodes() => _childNodes.Sort((a, b) => a.Entity.Transform._order.CompareTo(b.Entity.Transform._order));
    }

    public event Action<Hierarchy>? ChangedEvent;

    public string Name { get; }
    public IEnumerable<Node> RootNodes => _rootNodes;

    private readonly List<Node> _rootNodes = new();
    private readonly Dictionary<GameEntity, Node> _entityToNodeMap = new();

    public Hierarchy(Scene scene)
    {
        Name = scene.Name;
        
        foreach (var e in scene.Entities)
        {
            var n = CreateNodeFor(e, scene);
            
            if (e.Transform.HasParent()) continue;
            _rootNodes.Add(n);
        }
        
        SortHierarchyByTransformId();
    }

    public void AddNode(Node node)
    {
        _rootNodes.Add(node);
        ChangedEvent?.Invoke(this);
    }

    public void RemoveNodeWhere(Func<Node, bool> filter)
    {
        bool didChange = false;
		
        for (var i = _rootNodes.Count - 1; i >= 0; i--)
        {
            if (!filter(_rootNodes[i])) continue;
			
            _rootNodes.RemoveAt(i);
            didChange = true;
        }

        if (didChange)
        {
            ChangedEvent?.Invoke(this);
        }
    }

    private Node CreateNodeFor(GameEntity e, Scene scene)
    {
        if (_entityToNodeMap.ContainsKey(e)) return _entityToNodeMap[e];
            
        Node? parentNode = null;
        if (e.Transform.HasParent())
        {
            var parent = scene.GetById(e.Transform._parent);
            if (parent == null) throw new Exception("Parent is null. {e}");
            parentNode = _entityToNodeMap.ContainsKey(parent) ? _entityToNodeMap[parent] : CreateNodeFor(parent, scene);
        }
            
        var node = new Node(e, parentNode);
        parentNode?.AddNode(node);

        _entityToNodeMap.Add(e, node);
        return node;
    }
    
    private void SortHierarchyByTransformId()
    {
        _rootNodes.Sort((a, b) => a.Entity.Transform._order.CompareTo(b.Entity.Transform._order));

        void SortNodeRecursive(Node node)
        {
            node.SortNodes();
            foreach (var childNode in node.ChildNodes)
            {
                SortNodeRecursive(childNode);
            }
        }
        
        foreach (var n in _rootNodes)
        {
            SortNodeRecursive(n);
        }
    }
}