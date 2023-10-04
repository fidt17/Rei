using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Assets;

public static class AssetUtils
{
	public static readonly Dictionary<Type, string> AssetFileExtensions = new()
	{
		{ typeof(Scene), FileExtensions.SCENE },
		{ typeof(BuildScenesConfiguration), FileExtensions.ASSET }
	};
}