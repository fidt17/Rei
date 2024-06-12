using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Shaders;

public class ShaderAssetInfo
{
    
}

public interface IShadersRegistry
{
    Task RefreshShaders();
}

public class ShadersRegistry : IShadersRegistry
{
    public async Task RefreshShaders()
    {
        // get to engine resources folder
        
        // find all shader files
        // generate meta files
        // register shaders
    }
}