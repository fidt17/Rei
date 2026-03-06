namespace ReiEditor.Models.Services.Render;

public class ShaderUniformInfo
{
    public string Name { get; }
    public string SourceType { get; }
    public ShaderUniformType Type { get; }
    public bool IsSupported => Type != ShaderUniformType.Unsupported;

    public ShaderUniformInfo(string name, string sourceType, ShaderUniformType type)
    {
        Name = name;
        SourceType = sourceType;
        Type = type;
    }
}
