using System.Collections.Generic;
using ReiEditor.Models.Resources;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourAssetInfo
{
    public string BehaviourName { get; }
    public IEnumerable<string> SerializedProperties => _serializedProperties;
    
    public ObjectFile<BehaviourMeta> Meta { get; }
    public ObjectFile<string> Behaviour { get; }

    private readonly List<string> _serializedProperties;

    public BehaviourAssetInfo(string behaviourName, ObjectFile<BehaviourMeta> meta, ObjectFile<string> behaviour, List<string> serializedProperties)
    {
        BehaviourName = behaviourName;
        Meta = meta;
        Behaviour = behaviour;
        _serializedProperties = serializedProperties;
    }
}