using System.Collections.Generic;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Models.Services.Assets.Shaders;

public interface IShaderUniformParser
{
    IReadOnlyList<ShaderUniformInfo> ParseUniforms(string source);
}
