using System.Collections.Generic;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Scenes;

public class BuildScenesConfiguration : Asset
{
	[JsonProperty]
	public Dictionary<int, string> Scenes { get; } = new();
}