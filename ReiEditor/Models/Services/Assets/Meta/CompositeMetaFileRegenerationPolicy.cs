using System.Collections.Generic;

namespace ReiEditor.Models.Services.Assets.Meta;

public sealed class CompositeMetaFileRegenerationPolicy : IMetaFileRegenerationPolicy
{
    private readonly IReadOnlyList<IMetaFileRegenerationPolicy> _policies;

    public CompositeMetaFileRegenerationPolicy(params IMetaFileRegenerationPolicy[] policies)
    {
        _policies = policies;
    }

    public void Apply(AssetMeta meta, string assetPath)
    {
        foreach (var policy in _policies)
        {
            policy.Apply(meta, assetPath);
        }
    }
}
