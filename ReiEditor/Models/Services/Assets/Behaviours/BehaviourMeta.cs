namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourMeta : AssetMeta
{
    public int BehaviourId { get; set; }

    public BehaviourMeta(string id, AssetType type) : base(id, type)
    {
    }
}