using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Scenes;

public sealed class SceneAssetDropService : ISceneAssetDropService
{
    private readonly ISceneAssetDropTargetBuilderService _targetBuilderService;
    private readonly ISceneAssetPlacementService _placementService;
    private readonly ISceneAssetEntityInitializationService _entityInitializationService;

    public SceneAssetDropService(
        ISceneAssetDropTargetBuilderService targetBuilderService,
        ISceneAssetPlacementService placementService,
        ISceneAssetEntityInitializationService entityInitializationService)
    {
        _targetBuilderService = targetBuilderService;
        _placementService = placementService;
        _entityInitializationService = entityInitializationService;
    }

    public bool CanHandleAssetPaths(IReadOnlyList<string> assetPaths)
    {
        return _targetBuilderService.CanHandleAssetPaths(assetPaths);
    }

    public async Task<int> CreateEntitiesFromAssets(IReadOnlyList<string> assetPaths)
    {
        var targets = _targetBuilderService.BuildTargets(assetPaths);
        if (targets.Count == 0) return 0;

        var placements = _placementService.BuildPlacements(targets);
        var creationTasks = targets.Zip(placements, (target, placement) => _entityInitializationService.CreateEntityForAsset(target, placement));
        var creationResults = await Task.WhenAll(creationTasks);
        
        return creationResults.Count(created => created);
    }
}
