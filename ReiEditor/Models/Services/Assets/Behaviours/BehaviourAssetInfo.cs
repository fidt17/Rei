using System.Collections.Generic;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Behaviours.Types;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourAssetInfo
{
    public string BehaviourName { get; }
    public int BehaviourId { get; }
    
    public IReadOnlyDictionary<string, SerializedTypeEnum> SerializedProperties => _serializedProperties;
    
    public ObjectFile<string> Behaviour { get; }

    private readonly Dictionary<string, SerializedTypeEnum> _serializedProperties;

    public BehaviourAssetInfo(string behaviourName, int behaviourId, ObjectFile<string> behaviour, Dictionary<string, SerializedTypeEnum> serializedProperties)
    {
        BehaviourName = behaviourName;
        BehaviourId = behaviourId;
        Behaviour = behaviour;
        _serializedProperties = serializedProperties;
    }
}