using System.IO;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Build.Assets;

public class BuildScenedConfigurationAssetBuilder
{
	public static void Build(BinaryWriter writer, BuildScenesConfiguration configuration)
	{
		writer.Write(configuration.Scenes.Count);
		foreach (var keyValuePair in configuration.Scenes)
		{
			writer.Write(keyValuePair.Key);
			writer.WriteString(keyValuePair.Value);
		}
	}
}