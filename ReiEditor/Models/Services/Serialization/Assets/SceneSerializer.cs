using System.IO;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Serialization.Assets;

public class SceneSerializer : IBinarySerializer<Scene>
{
	public void Serialize(Scene target, BinaryWriter writer)
	{
		writer.WriteString(target.Name);
	}
}