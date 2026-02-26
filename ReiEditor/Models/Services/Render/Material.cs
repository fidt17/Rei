using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Render;

public class Material : Asset
{
    [JsonProperty("ShaderAssetId")]
    public string ShaderAssetId { get; private set; } = "";

    public Material() { }

    public Material(string shaderAssetId)
    {
        ShaderAssetId = shaderAssetId;
    }
}
