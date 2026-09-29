namespace ReiEditor.Models.Services.Assets.Meta;

public interface IMetaFileRegenerationPolicy
{
    void Apply(AssetMeta meta, string assetPath);
}
