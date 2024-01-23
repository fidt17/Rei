namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourMeta : AssetMeta
{
    public int BehaviourId { get; }

    public BehaviourMeta(int behaviourId, string id, AssetType type) : base(id, type)
    {
        BehaviourId = behaviourId;
    }
}