using System;

namespace ReiEditor.Models.Services.Assets;

public class AssetInfo
{
	public string Id { get; }
	public string FullPath { get; }
	public Type AssetType { get; }

	public AssetInfo(string id, string fullPath, Type assetType)
	{
		Id = id;
		FullPath = fullPath;
		AssetType = assetType;
	}
}