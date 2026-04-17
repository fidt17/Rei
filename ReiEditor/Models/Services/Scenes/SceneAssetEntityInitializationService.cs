using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.SerializedProperties;

namespace ReiEditor.Models.Services.Scenes;

public sealed class SceneAssetEntityInitializationService : ISceneAssetEntityInitializationService
{
    private readonly ILogger<SceneAssetEntityInitializationService> _logger;
    private readonly IEntityManagementService _entityManagementService;
    private readonly IEntityDataWriterService _entityDataWriterService;
    private readonly IBehaviourRegistry _behaviourRegistry;

    public SceneAssetEntityInitializationService(
        ILogger<SceneAssetEntityInitializationService> logger,
        IEntityManagementService entityManagementService,
        IEntityDataWriterService entityDataWriterService,
        IBehaviourRegistry behaviourRegistry)
    {
        _logger = logger;
        _entityManagementService = entityManagementService;
        _entityDataWriterService = entityDataWriterService;
        _behaviourRegistry = behaviourRegistry;
    }

    public async Task<bool> CreateEntityForAsset(SceneAssetDropTarget target, SceneAssetDropPlacement placement)
    {
        try
        {
            var entity = await _entityManagementService.CreateEntity(target.EntityName);
            if (entity == null) return false;

            var behaviourName = GetBehaviourName(target.AssetType);
            var behaviourId = _behaviourRegistry.GetIdByName(behaviourName);
            if (behaviourId == null)
            {
                _logger.LogError($"Could not find behaviour id for {behaviourName}");
                return false;
            }

            _entityManagementService.AddBehaviour(entity, behaviourId.Value);
            ApplyDropTransform(entity, placement);

            var assetAssigned = _entityDataWriterService.SetBehaviourProperty(
                entity,
                behaviourId.Value,
                GetAssetPropertyName(target.AssetType),
                new Dictionary<string, object?>
                {
                    { EngineBehavioursConstants.ASSET_REF_ID, target.AssetId }
                });
            if (!assetAssigned)
            {
                _logger.LogError($"Could not assign asset {target.AssetId} to {behaviourName} on entity {entity.Id}");
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
            return false;
        }
    }

    private static string GetBehaviourName(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Model => EngineBehavioursConstants.MESH_RENDERER,
            AssetType.Texture => EngineBehavioursConstants.SPRITE_RENDERER,
            _ => throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null)
        };
    }

    private static string GetAssetPropertyName(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Model => EngineBehavioursConstants.MESH_RENDERER_MODEL,
            AssetType.Texture => EngineBehavioursConstants.SPRITE_RENDERER_SPRITE,
            _ => throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null)
        };
    }

    private void ApplyDropTransform(GameEntity entity, SceneAssetDropPlacement placement)
    {
        var transformBehaviourId = _behaviourRegistry.GetIdByName(EngineBehavioursConstants.TRANSFORM);
        if (transformBehaviourId == null)
        {
            _logger.LogError("Scene asset drop could not find Transform behaviour id");
            return;
        }

        var positionAssigned = _entityDataWriterService.SetBehaviourProperty(
            entity,
            transformBehaviourId.Value,
            EngineBehavioursConstants.TRANSFORM_POSITION,
            SerializedPropertyVector3Utility.CreateVector3Value(placement.Position));
        var rotationAssigned = _entityDataWriterService.SetBehaviourProperty(
            entity,
            transformBehaviourId.Value,
            EngineBehavioursConstants.TRANSFORM_ROTATION,
            SerializedPropertyVector3Utility.CreateVector3Value(placement.Rotation));

        if (!positionAssigned || !rotationAssigned)
        {
            _logger.LogError($"Scene asset drop could not assign transform on entity {entity.Id}");
        }
    }
}
