using ReiEditor.Models.Services.Assets.Meta;

namespace ReiEditor.Models.Services.Assets;

public class AssetInfo
{
	public string FullPath { get; }
	public AssetMeta Meta { get; }

	public AssetInfo(AssetMeta meta, string fullPath)
	{
		Meta = meta;
		FullPath = fullPath;
	}

	public override string ToString()
	{
		return $"{Meta.AssetId} - {FullPath}";
	}
}