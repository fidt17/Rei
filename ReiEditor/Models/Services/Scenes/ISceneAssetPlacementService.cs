using System.Collections.Generic;

namespace ReiEditor.Models.Services.Scenes;

public interface ISceneAssetPlacementService
{
    IReadOnlyList<SceneAssetDropPlacement> BuildPlacements(IReadOnlyList<SceneAssetDropTarget> targets);
}
