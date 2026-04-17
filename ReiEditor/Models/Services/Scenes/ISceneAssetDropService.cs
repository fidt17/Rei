using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Scenes;

public interface ISceneAssetDropService
{
    bool CanHandleAssetPaths(IReadOnlyList<string> assetPaths);
    Task<int> CreateEntitiesFromAssets(IReadOnlyList<string> assetPaths);
}
