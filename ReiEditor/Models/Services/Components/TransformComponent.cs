using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Components;

public class TransformComponent
{
    public event Action<int>? OrderChangedEvent;
    
    [JsonIgnore] public int Parent => _parent;
    [JsonIgnore] public int Order => _order;
    [JsonIgnore] public IEnumerable<int> Children => _children;

    [JsonProperty("Parent")]
    private int _parent;

    [JsonProperty("Order")]
    private int _order;
    
    [JsonProperty("Children")]
    private List<int> _children { get; } = new();

    public bool HasParent() => _parent != 0;

    public void AddChild(int value) => _children.Add(value);
    public void RemoveChild(int value) => _children.Remove(value);

    public void SetParent(int parent) => _parent = parent;
    
    public void SetOrder(int value)
    {
        if (_order == value) return;
        
        _order = value;
        OrderChangedEvent?.Invoke(_order);
    }
}