namespace ReiEditor.Models.Services.Assets.Meta;

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
