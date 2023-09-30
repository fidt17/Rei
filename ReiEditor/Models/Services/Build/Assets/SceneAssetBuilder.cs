using System.IO;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Build.Assets;

public class SceneAssetBuilder
{
	public static void Build(BinaryWriter writer, Scene scene)
	{
		writer.Write(32);
	}
}