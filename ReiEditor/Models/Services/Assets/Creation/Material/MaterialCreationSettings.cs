namespace ReiEditor.Models.Services.Assets.Creation.Material;

public readonly struct MaterialCreationSettings
{
    public string TargetDirectory { get; }
    public string MaterialName { get; }
    public string ShaderAssetId { get; }

    public MaterialCreationSettings(
        string targetDirectory,
        string materialName,
        string shaderAssetId)
    {
        TargetDirectory = targetDirectory;
        MaterialName = materialName.Trim();
        ShaderAssetId = shaderAssetId.Trim();
    }
}
