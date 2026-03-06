using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Creation.Material;

public interface IMaterialCreationUtility
{
    Task<bool> CreateMaterialAsync(MaterialCreationSettings settings);
}
