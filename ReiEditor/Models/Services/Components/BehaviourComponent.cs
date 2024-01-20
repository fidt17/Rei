using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Components;

public class BehaviourComponent
{
    [JsonIgnore]
    public int Id => _id;
    
    [JsonProperty("Id")]
    private readonly int _id;

    public BehaviourComponent(int id)
    {
        _id = id;
    }
}