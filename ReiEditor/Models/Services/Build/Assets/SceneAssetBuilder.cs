using System.IO;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Build.Assets;

public class SceneAssetBuilder
{
	public static void Build(BinaryWriter writer, Scene scene)
	{
		writer.WriteString(scene.Name);
	}
}