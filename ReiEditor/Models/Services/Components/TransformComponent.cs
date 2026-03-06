using System;

namespace ReiEditor.Models.Services.Components;

public class TransformComponent
{
    public event Action<int>? OrderChangedEvent;
    
    public int Parent { get; private set; }
    public int Order { get; private set; }

    public bool HasParent() => Parent != 0;

    public void SetParent(int parent) => Parent = parent;
    
    public void SetOrder(int value)
    {
        if (Order == value) return;
        
        Order = value;
        OrderChangedEvent?.Invoke(Order);
    }
}
