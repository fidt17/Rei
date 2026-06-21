using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Render;

public class Shader : Asset
{
    public string Name { get; private set; } = "";
    public IReadOnlyList<ShaderUniformInfo> Uniforms => _uniforms;

    private readonly List<ShaderUniformInfo> _uniforms = new();

    public void SetName(string name)
    {
        Name = name;
    }

    public void SetUniforms(IEnumerable<ShaderUniformInfo> uniforms)
    {
        _uniforms.Clear();
        _uniforms.AddRange(uniforms.Where(x => x != null));
    }
}
