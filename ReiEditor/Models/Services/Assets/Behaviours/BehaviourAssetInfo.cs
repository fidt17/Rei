using System.Collections.Generic;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Behaviours.Types;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourAssetInfo
{
    public string Namespace { get; }
    public string BehaviourName { get; }
    public int BehaviourId { get; }
    public bool IsEngineBehaviour { get; }
    
    public IReadOnlyDictionary<string, SerializedTypeEnum> SerializedProperties => _serializedProperties;
    
    public ObjectFile<string> Behaviour { get; }

    private readonly Dictionary<string, SerializedTypeEnum> _serializedProperties;

    public BehaviourAssetInfo(string behaviourNamespace, string behaviourName, int behaviourId, ObjectFile<string> behaviour, Dictionary<string, SerializedTypeEnum> serializedProperties, bool isEngineBehaviour)
    {
        Namespace = behaviourNamespace;
        BehaviourName = behaviourName;
        BehaviourId = behaviourId;
        Behaviour = behaviour;
        _serializedProperties = serializedProperties;
        IsEngineBehaviour = isEngineBehaviour;
    }
}