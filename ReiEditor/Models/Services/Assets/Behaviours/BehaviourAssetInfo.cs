using System.Collections.Generic;
using ReiEditor.Models.Resources;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourAssetInfo
{
    public string BehaviourName { get; }
    public int BehaviourId { get; }
    public IEnumerable<string> SerializedProperties => _serializedProperties;
    
    public ObjectFile<string> Behaviour { get; }

    private readonly List<string> _serializedProperties;

    public BehaviourAssetInfo(string behaviourName, int behaviourId, ObjectFile<string> behaviour, List<string> serializedProperties)
    {
        BehaviourName = behaviourName;
        BehaviourId = behaviourId;
        Behaviour = behaviour;
        _serializedProperties = serializedProperties;
    }
}