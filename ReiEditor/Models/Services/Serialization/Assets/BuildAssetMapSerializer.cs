using System.IO;
using System.Linq;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Serialization.Assets;

public class BuildAssetMapSerializer : IBinarySerializer<BuildAssetMap>
{
	public void Serialize(BuildAssetMap target, BinaryWriter writer)
	{
		var assets = target.Assets.ToList();
		
		writer.Write(assets.Count);
		
		foreach (var assetBuildInfo in assets)
		{
			writer.WriteString(assetBuildInfo.Id);
			writer.WriteString(assetBuildInfo.Name);
			writer.WriteString(assetBuildInfo.Path);
			writer.Write(assetBuildInfo.Offset);
		}
	}
}