using System;

namespace ReiEditor.Models.Services.Assets.Meta;

public interface IMetaFileRegenerationPolicy
{
    void Apply(AssetMeta meta, string assetPath);
}

public sealed class DefaultMetaFileRegenerationPolicy : IMetaFileRegenerationPolicy
{
    public static DefaultMetaFileRegenerationPolicy Instance { get; } = new(); // cached instance for reuse

    private DefaultMetaFileRegenerationPolicy()
    {
    }

    public void Apply(AssetMeta meta, string assetPath)
    {
    }
}

public sealed class BehaviourMetaFileRegenerationPolicy : IMetaFileRegenerationPolicy
{
    private readonly Func<int> _allocateBehaviourId;

    public BehaviourMetaFileRegenerationPolicy(Func<int> allocateBehaviourId)
    {
        _allocateBehaviourId = allocateBehaviourId;
    }

    public void Apply(AssetMeta meta, string assetPath)
    {
        if (meta.TryGetData(BehaviourMeta.Key, out BehaviourMeta? _))
        {
            meta.AddData(BehaviourMeta.Key, new BehaviourMeta(_allocateBehaviourId()));
        }
    }
}
