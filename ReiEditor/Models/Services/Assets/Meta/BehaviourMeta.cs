using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Assets.Meta;

public class BehaviourMeta
{
    public static string Key => "BehaviourMeta";
    
    [JsonProperty("BehaviourId")]
    public int BehaviourId { get; }

    public BehaviourMeta(int behaviourId)
    {
        BehaviourId = behaviourId;
    }
}