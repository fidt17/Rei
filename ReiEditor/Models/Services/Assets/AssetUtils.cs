using System;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Assets;

public static class AssetUtils
{
	// todo: readonly static maps
	
	public static AssetType GetAssetType(Asset asset)
	{
		if (asset is Scene) return AssetType.Scene;

		return AssetType.Data;
	}

	public static Type GetAssetType(AssetType type)
	{
		return type switch
		{
			AssetType.Data => typeof(object),
			AssetType.Scene => typeof(Scene),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
	}

	public static bool TryGetAssetType(string extension, out AssetType type)
	{
		type = AssetType.Data;

		switch (extension)
		{
			case FileExtensions.SCENE:
				type = AssetType.Scene;
				return true;
			
			case FileExtensions.ASSET:
				type = AssetType.Data;
				return true;
		}
		
		return false;
	}

	public static string GetExtensionForAssetType(AssetType type)
	{
		return type switch
		{
			AssetType.Data => FileExtensions.ASSET,
			AssetType.Scene => FileExtensions.SCENE,
			AssetType.Behaviour => FileExtensions.H,
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
	}
}