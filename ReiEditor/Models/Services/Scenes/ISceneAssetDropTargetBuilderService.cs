using System.Collections.Generic;

namespace ReiEditor.Models.Services.Scenes;

public interface ISceneAssetDropTargetBuilderService
{
    bool CanHandleAssetPaths(IReadOnlyList<string> assetPaths);
    IReadOnlyList<SceneAssetDropTarget> BuildTargets(IReadOnlyList<string> assetPaths);
}
