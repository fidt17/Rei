using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Scenes;

public interface ISceneAssetEntityInitializationService
{
    Task<bool> CreateEntityForAsset(SceneAssetDropTarget target, SceneAssetDropPlacement placement);
}
