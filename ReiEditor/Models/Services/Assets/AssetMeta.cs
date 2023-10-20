using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Assets;

public class AssetMeta
{
	[JsonProperty]
	public string Id { get; }
	
	[JsonProperty]
	public AssetType Type { get; }

	public AssetMeta(string id, AssetType type)
	{
		Id = id;
		Type = type;
	}
}