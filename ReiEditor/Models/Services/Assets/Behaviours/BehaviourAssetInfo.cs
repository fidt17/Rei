using ReiEditor.Models.Resources;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourAssetInfo
{
    public string BehaviourName { get; }
    public ObjectFile<BehaviourMeta> Meta { get; }
    public ObjectFile<string> Behaviour { get; }

    public BehaviourAssetInfo(string behaviourName, ObjectFile<BehaviourMeta> meta, ObjectFile<string> behaviour)
    {
        BehaviourName = behaviourName;
        Meta = meta;
        Behaviour = behaviour;
    }
}