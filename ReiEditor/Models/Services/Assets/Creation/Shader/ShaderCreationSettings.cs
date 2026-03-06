namespace ReiEditor.Models.Services.Assets.Creation.Shader;

public readonly struct ShaderCreationSettings
{
    public string TargetDirectory { get; }
    public string ShaderName { get; }

    public ShaderCreationSettings(string targetDirectory, string shaderName)
    {
        TargetDirectory = targetDirectory;
        ShaderName = shaderName.Trim();
    }
}
