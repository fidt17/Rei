using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Assets;

public class Asset
{
	[JsonProperty]
	public string Id { get; }

	[JsonProperty]
	public string Name { get; }

	public Asset(string id, string name)
	{
		Id = id;
		Name = name;
	}
}