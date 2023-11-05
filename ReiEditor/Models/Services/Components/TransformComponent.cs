using System.Collections.Generic;
using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Components;

public class TransformComponent
{
    [JsonProperty("Parent")]
    public int _parent;

    [JsonProperty("Order")]
    public int _order;
    
    [JsonProperty("Children")]
    public List<int> _children { get; } = new List<int>();

    public bool HasParent() => _parent != 0;
}