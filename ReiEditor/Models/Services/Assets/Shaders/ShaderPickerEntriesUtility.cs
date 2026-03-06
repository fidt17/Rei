using System.Collections.Generic;
using System.Linq;
using ReiEditor.ViewModels.Controls.Assets;

namespace ReiEditor.Models.Services.Assets.Shaders;

public static class ShaderPickerEntriesUtility
{
    public static IReadOnlyList<AssetPickerViewModel.Entry> BuildEntries(this IShaderRegistry shaderRegistry)
    {
        return shaderRegistry.Shaders.Values
            .Select(shader => new AssetPickerViewModel.Entry(shader.Name, shader.FullPath, shader.AssetId))
            .OrderBy(x => x.Name)
            .ToList();
    }
}
