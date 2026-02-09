using System;

namespace ReiEditor.Models.Services.Hierarchies;

public interface IHierarchyProvider<T> where T : notnull
{
    public event Action? HierarchyRebuiltEvent;
    
    Hierarchy<T>  Hierarchy { get; }
}