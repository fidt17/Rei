using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using System.Collections.Generic;

namespace ReiEditor.Models.Services.Render;

public class Material : Asset
{
    [JsonProperty("ShaderAssetId")]
    public string ShaderAssetId { get; private set; } = "";

    [JsonProperty("UseDepth")]
    public bool UseDepth { get; private set; } = true;

    [JsonProperty("SortingOrder")]
    public int SortingOrder { get; private set; } = 1000;

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

    public void SetUseDepth(bool value)
    {
        UseDepth = value;
    }

    public void SetSortingOrder(int value)
    {
        SortingOrder = value;
    }
}
