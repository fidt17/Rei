using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Scenes;

public class Scene : Asset
{
	[JsonProperty]
	public string Name { get; }

	public Scene(string name)
	{
		Name = name;
	}
}