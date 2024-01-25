using System.Collections.Generic;
using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Components;

public class BehaviourComponent
{
    [JsonIgnore]
    public int Id => _id;
    
    [JsonProperty("SerializedData")]
    public readonly Dictionary<string, object> SerializedData = new();
    
    [JsonProperty("Id")]
    private readonly int _id;

    public BehaviourComponent(int id)
    {
        _id = id;
    }
}