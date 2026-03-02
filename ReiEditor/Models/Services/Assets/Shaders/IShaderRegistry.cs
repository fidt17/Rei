using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Models.Services.Assets.Shaders;

public interface IShaderRegistry
{
    IReadOnlyDictionary<string, Shader> Shaders { get; }

    bool TryGetById(string assetId, [NotNullWhen(returnValue: true)] out Shader? shader);
    Task RefreshShaders();
}
