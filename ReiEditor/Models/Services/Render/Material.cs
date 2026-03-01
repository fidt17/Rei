using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using System.Collections.Generic;

namespace ReiEditor.Models.Services.Render;

public class Material : Asset
{
    [JsonProperty("ShaderAssetId")]
    public string ShaderAssetId { get; private set; } = "";

    [JsonProperty("Properties")]
    public Dictionary<string, object?> Properties { get; private set; } = new();

    public Material() { }

    public Material(string shaderAssetId)
    {
        ShaderAssetId = shaderAssetId;
    }

    public void SetShaderAssetId(string shaderAssetId)
    {
        ShaderAssetId = shaderAssetId?.Trim() ?? "";
    }
}
