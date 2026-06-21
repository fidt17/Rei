using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.SerializedProperties;

namespace ReiEditor.Models.Services.Scenes;

public sealed class SceneAssetPlacementService : ISceneAssetPlacementService
{
    private sealed record DropCameraSnapshot(Vector3 Position, Vector3 Rotation, Vector3 Forward, Vector3 Right);

    private const float DROP_DISTANCE = 10.0f;
    private const float DROP_SPACING = 2.0f;

    private readonly ILogger<SceneAssetPlacementService> _logger;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IBehaviourRegistry _behaviourRegistry;

    public SceneAssetPlacementService(
        ILogger<SceneAssetPlacementService> logger,
        ISceneManagementService sceneManagementService,
        IBehaviourRegistry behaviourRegistry)
    {
        _logger = logger;
        _sceneManagementService = sceneManagementService;
        _behaviourRegistry = behaviourRegistry;
    }

    public IReadOnlyList<SceneAssetDropPlacement> BuildPlacements(IReadOnlyList<SceneAssetDropTarget> targets)
    {
        if (targets.Count == 0) return Array.Empty<SceneAssetDropPlacement>();

        var cameraSnapshot = GetDropCameraSnapshot();
        return targets
            .Select((target, index) => BuildPlacement(target.AssetType, cameraSnapshot, index, targets.Count))
            .ToList();
    }

    private DropCameraSnapshot GetDropCameraSnapshot()
    {
        var scene = _sceneManagementService.CurrentScene.Value;
        if (scene == null)
        {
            _logger.LogWarning("Scene asset drop could not capture camera snapshot because current scene is missing");
            return CreateFallbackCameraSnapshot();
        }

        var cameraBehaviourId = _behaviourRegistry.GetIdByName(EngineBehavioursConstants.CAMERA);
        var transformBehaviourId = _behaviourRegistry.GetIdByName(EngineBehavioursConstants.TRANSFORM);
        if (cameraBehaviourId == null || transformBehaviourId == null)
        {
            _logger.LogWarning("Scene asset drop could not capture camera snapshot because required behaviour ids are missing");
            return CreateFallbackCameraSnapshot();
        }

        var cameraEntity = scene.Entities.FirstOrDefault(entity => entity.GetBehaviour(cameraBehaviourId.Value) != null);
        if (cameraEntity == null)
        {
            _logger.LogWarning("Scene asset drop could not find a camera entity; using fallback snapshot");
            return CreateFallbackCameraSnapshot();
        }

        var transformBehaviour = cameraEntity.GetBehaviour(transformBehaviourId.Value);
        if (transformBehaviour == null)
        {
            _logger.LogWarning($"Scene asset drop camera entity {cameraEntity.Id} has no Transform behaviour; using fallback snapshot");
            return CreateFallbackCameraSnapshot();
        }

        if (!SerializedPropertyVector3Utility.TryGetVector3Property(transformBehaviour, EngineBehavioursConstants.TRANSFORM_POSITION, out var position) ||
            !SerializedPropertyVector3Utility.TryGetVector3Property(transformBehaviour, EngineBehavioursConstants.TRANSFORM_ROTATION, out var rotation))
        {
            _logger.LogWarning($"Scene asset drop camera entity {cameraEntity.Id} is missing transform data; using fallback snapshot");
            return CreateFallbackCameraSnapshot();
        }

        var forward = CalculateForwardVector(rotation);
        var right = CalculateRightVector(forward);
        return new DropCameraSnapshot(position, rotation, forward, right);
    }

    private static DropCameraSnapshot CreateFallbackCameraSnapshot()
    {
        var forward = Vector3.UnitZ;
        return new DropCameraSnapshot(Vector3.Zero, Vector3.Zero, forward, Vector3.UnitX);
    }

    private static SceneAssetDropPlacement BuildPlacement(AssetType assetType, DropCameraSnapshot cameraSnapshot, int index, int totalCount)
    {
        var horizontalOffset = (index - ((totalCount - 1) * 0.5f)) * DROP_SPACING;
        var position = cameraSnapshot.Position + cameraSnapshot.Forward * DROP_DISTANCE + cameraSnapshot.Right * horizontalOffset;
        var rotation = assetType == AssetType.Texture ? cameraSnapshot.Rotation : Vector3.Zero;
        return new SceneAssetDropPlacement(position, rotation);
    }

    private static Vector3 CalculateForwardVector(Vector3 rotationDegrees)
    {
        var pitchRadians = DegreesToRadians(rotationDegrees.X);
        var yawRadians = DegreesToRadians(rotationDegrees.Y);

        var forward = new Vector3(
            MathF.Sin(yawRadians) * MathF.Cos(pitchRadians),
            -MathF.Sin(pitchRadians),
            MathF.Cos(yawRadians) * MathF.Cos(pitchRadians));
        return forward.LengthSquared() <= float.Epsilon ? Vector3.UnitZ : Vector3.Normalize(forward);
    }

    private static Vector3 CalculateRightVector(Vector3 forward)
    {
        var right = Vector3.Cross(Vector3.UnitY, forward);
        return right.LengthSquared() <= float.Epsilon ? Vector3.UnitX : Vector3.Normalize(right);
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180.0f);
    }
}
