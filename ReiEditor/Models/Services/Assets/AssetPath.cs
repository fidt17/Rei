using System;

namespace ReiEditor.Models.Services.Assets;

public class AssetPath
{
	public string FullPath { get; }
	public Type AssetType { get; }

	public AssetPath(string fullPath, Type assetType)
	{
		FullPath = fullPath;
		AssetType = assetType;
	}
}