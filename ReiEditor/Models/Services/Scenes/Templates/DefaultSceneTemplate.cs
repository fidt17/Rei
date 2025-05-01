using System.Collections.Generic;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.Scenes.Templates;

public class DefaultSceneTemplate : ISceneTemplate
{
    private readonly IEntityManagementService _entityManagementService;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IBehaviourRegistry _behaviourRegistry;

    public DefaultSceneTemplate(IEntityManagementService entityManagementService, IBehaviourComponentsService behaviourComponentsService, IBehaviourRegistry behaviourRegistry)
    {
        _entityManagementService = entityManagementService;
        _behaviourComponentsService = behaviourComponentsService;
        _behaviourRegistry = behaviourRegistry;
    }

    public void SetupScene()
    {
        var mainCamera = _entityManagementService.CreateEntity("Main Camera");
        if (mainCamera != null)
        {
            _behaviourComponentsService.AddComponent(mainCamera, EngineBehavioursUtility.CAMERA);
            _behaviourComponentsService.AddComponent(mainCamera, EngineBehavioursUtility.AMBIENT_LIGHT);

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
        }

        var pointLight = _entityManagementService.CreateEntity("Point Light");
        if (pointLight != null)
        {
            _behaviourComponentsService.AddComponent(pointLight, EngineBehavioursUtility.POINT_LIGHT);
        }
    }
}