using System;
using System.Collections.Generic;
using System.Linq;

namespace ReiEditor.Models.Services.Hierarchies;

public class Hierarchy<T> where T : notnull
{
    public event Action<HierarchyNode<T>>? NodeAddedEvent;
    public event Action<HierarchyNode<T>>? NodeRemovedEvent;

    public delegate void NodeMovedDelegate(HierarchyNode<T> node, HierarchyNode<T>? oldParent, int oldOrder, int newOrder);
    public event NodeMovedDelegate? NodeMovedEvent;

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

    public void AddNode(HierarchyNode<T> node, bool isRoot)
    {
        _nodeMap.Add(node.Content, node);

        if (isRoot)
        {
            _rootNodes.Add(node);
        }
        
        NodeAddedEvent?.Invoke(node);
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

        RemoveNodeFromMap(node);
        
        NodeRemovedEvent?.Invoke(node);
    }

    public bool MoveNode(HierarchyNode<T> node, HierarchyNode<T>? parent, int order)
    {
        if (node == parent) return false;
        if (GetAllChildNodes(node).Contains(parent)) return false;

        var oldParent = node.Parent;
        var oldOrder = GetNodeOrder(node);
        // Event consumers apply the removal offset to the requested insertion index themselves.
        var newOrder = order;
        if (oldParent == parent && oldOrder == order) return false;

        var targetListCount = parent == null ? _rootNodes.Count : parent.ChildNodes.Count();
        order = Math.Clamp(order, 0, targetListCount);
        if (oldParent == parent && oldOrder < order) order -= 1;

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
        
        NodeMovedEvent?.Invoke(node, oldParent, oldOrder, newOrder);
        
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
    
    public void SortRootNodes(Func<HierarchyNode<T>, HierarchyNode<T>, int> comparison) => _rootNodes.Sort((a,b) => comparison(a, b));

    private void RemoveNodeFromMap(HierarchyNode<T> node)
    {
        foreach (var child in node.ChildNodes.ToArray())
        {
            RemoveNodeFromMap(child);
        }

        _nodeMap.Remove(node.Content);
    }
}
