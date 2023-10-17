using System.IO;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Serialization.Assets;

public class BuildScenesConfigSerializer : IBinarySerializer<BuildScenesConfiguration>
{
	public void Serialize(BuildScenesConfiguration target, BinaryWriter writer)
	{
		writer.Write(target.Scenes.Count);
		foreach (var keyValuePair in target.Scenes)
		{
			writer.Write(keyValuePair.Key);
			writer.WriteString(keyValuePair.Value);
		}
	}
}