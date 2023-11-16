using System;
using System.Collections.Generic;
using System.Linq;

namespace ReiEditor.Models.Services.Hierarchies;

public class Hierarchy<T> where T : notnull
{
    public event Action<Hierarchy<T>>? ChangedEvent;

    public string Name { get; }
    public IEnumerable<HierarchyNode<T>> RootNodes => _rootNodes;

    private readonly List<HierarchyNode<T>> _rootNodes = new();
    private readonly Dictionary<T, HierarchyNode<T>> _nodeMap = new();

    public Hierarchy(string name)
    {
        Name = name;
    }

    public HierarchyNode<T>? GetNode(T content)
    {
        if (!_nodeMap.ContainsKey(content)) return null;
        return _nodeMap[content];
    }

    public int GetNodeOrder(HierarchyNode<T> node)
    {
        return node.Parent?.GetChildIdx(node) ?? _rootNodes.IndexOf(node);
    }

    public void AddRootNode(HierarchyNode<T> node)
    {
        _rootNodes.Add(node);
        _nodeMap.Add(node.Content, node);
        
        ChangedEvent?.Invoke(this);
    }

    public void DeleteNode(HierarchyNode<T> node)
    {
        if (node.Parent == null)
        {
            _rootNodes.Remove(node);
        }
        else
        {
            node.Parent.RemoveChild(node);
        }
        
        ChangedEvent?.Invoke(this);
    }

    public bool MoveNode(HierarchyNode<T> node, HierarchyNode<T>? parent, int order)
    {
        if (node == parent) return false;
        if (GetAllChildNodes(node).Contains(parent)) return false;

        if (node.Parent == parent)
        {
            int currentIdx = parent?.GetChildIdx(node) ?? _rootNodes.IndexOf(node);
            if (currentIdx == order) return false;
            
            if (currentIdx < order)
            {
                order -= 1;
            }
        }

        if (node.Parent == null)
        {
            _rootNodes.Remove(node);
        }
        else
        {
            node.Parent.RemoveChild(node);
        }
        
        if (parent == null)
        {
            _rootNodes.Insert(order, node);
            node.SetParent(null);
        }
        else
        {
            parent.AddChild(node, order);
            node.SetParent(parent);
        }
        
        ChangedEvent?.Invoke(this);
        
        return true;
    }

    public IEnumerable<HierarchyNode<T>> GetAllChildNodes(HierarchyNode<T> parent)
    {
        foreach (var child in parent.ChildNodes)
        {
            yield return child;

            foreach (var secondChildren in GetAllChildNodes(child))
            {
                yield return secondChildren;
            }
        }
    }
}