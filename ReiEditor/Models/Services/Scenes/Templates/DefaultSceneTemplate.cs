using System.Collections.Generic;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.Scenes.Templates;

public class DefaultSceneTemplate : ISceneTemplate
{
    private readonly IEntityManagementService _entityManagementService;
    private readonly IBehaviourRegistry _behaviourRegistry;

    public DefaultSceneTemplate(IEntityManagementService entityManagementService, IBehaviourRegistry behaviourRegistry)
    {
        _entityManagementService = entityManagementService;
        _behaviourRegistry = behaviourRegistry;
    }

    public void SetupScene()
    {
        var mainCamera = _entityManagementService.CreateEntity("Main Camera");
        if (mainCamera != null)
        {
            _entityManagementService.AddBehaviour(mainCamera, _behaviourRegistry.GetIdByName(EngineBehavioursUtility.CAMERA)!.Value);
            _entityManagementService.AddBehaviour(mainCamera, _behaviourRegistry.GetIdByName(EngineBehavioursUtility.AMBIENT_LIGHT)!.Value);

            var transform = mainCamera.GetBehaviour(_behaviourRegistry.GetIdByName(EngineBehavioursUtility.TRANSFORM));
            if (transform != null)
            {
                if (transform.GetProperty(EngineBehavioursUtility.TRANSFORM_POSITION).Value is Dictionary<string, SerializedProperty> position)
                {
                    position["x"].Value = 0;
                    position["y"].Value = 2;
                    position["z"].Value = -10;
                }
                
                if (transform.GetProperty(EngineBehavioursUtility.TRANSFORM_ROTATION).Value is Dictionary<string, SerializedProperty> rotation)
                {
                    rotation["x"].Value = 0;
                    rotation["y"].Value = -15;
                    rotation["z"].Value = 0;
                }
            }
            
            var camera = mainCamera.GetBehaviour(_behaviourRegistry.GetIdByName(EngineBehavioursUtility.CAMERA));
            if (camera != null)
            {
                if (camera.GetProperty(EngineBehavioursUtility.CAMERA_BACKGROUND_COLOR).Value is Dictionary<string, SerializedProperty> backgroundColor)
                {
                    backgroundColor["r"].Value = 0.074;
                    backgroundColor["g"].Value = 0.090;
                    backgroundColor["b"].Value = 0.116;
                    backgroundColor["a"].Value = 1;
                }
            }
        }

        var pointLight = _entityManagementService.CreateEntity("Point Light");
        if (pointLight != null)
        {
            _entityManagementService.AddBehaviour(pointLight, _behaviourRegistry.GetIdByName(EngineBehavioursUtility.POINT_LIGHT)!.Value);
        }
    }
}