using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Creation.Shader;

public interface IShaderCreationUtility
{
    Task<bool> CreateShaderAsync(ShaderCreationSettings settings);
}
