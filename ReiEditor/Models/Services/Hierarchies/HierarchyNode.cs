using System.Collections.Generic;

namespace ReiEditor.Models.Services.Hierarchies;

public class HierarchyNode<T>
{
    public HierarchyNode<T>? Parent { get; private set; }
    public T Content { get; }

    public IEnumerable<HierarchyNode<T>> ChildNodes => _childNodes;

    private readonly List<HierarchyNode<T>> _childNodes = new();

    public HierarchyNode(T content, HierarchyNode<T>? parent)
    {
        Content = content;
        Parent = parent;
    }

    public void SetParent(HierarchyNode<T>? parent) => Parent = parent;

    public void AddChild(HierarchyNode<T> node, int idx) => _childNodes.Insert(idx, node);
    public void RemoveChild(HierarchyNode<T> node) => _childNodes.Remove(node);
    public int GetChildIdx(HierarchyNode<T> node) => _childNodes.IndexOf(node);
}