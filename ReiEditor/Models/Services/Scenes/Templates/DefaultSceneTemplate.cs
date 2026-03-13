using System.Collections.Generic;
using System.Threading.Tasks;
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

    public async Task SetupScene()
    {
        var mainCamera = await _entityManagementService.CreateEntity("Main Camera");
        if (mainCamera != null)
        {
            _entityManagementService.AddBehaviour(mainCamera, _behaviourRegistry.GetIdByName(EngineBehavioursConstants.CAMERA)!.Value);
            _entityManagementService.AddBehaviour(mainCamera, _behaviourRegistry.GetIdByName(EngineBehavioursConstants.AMBIENT_LIGHT)!.Value);

            var transform = mainCamera.GetBehaviour(_behaviourRegistry.GetIdByName(EngineBehavioursConstants.TRANSFORM));
            if (transform != null)
            {
                if (transform.GetProperty(EngineBehavioursConstants.TRANSFORM_POSITION).Value is Dictionary<string, SerializedProperty> position)
                {
                    position["x"].Value = 0;
                    position["y"].Value = 2;
                    position["z"].Value = -10;
                }
                
                if (transform.GetProperty(EngineBehavioursConstants.TRANSFORM_ROTATION).Value is Dictionary<string, SerializedProperty> rotation)
                {
                    rotation["x"].Value = 0;
                    rotation["y"].Value = -15;
                    rotation["z"].Value = 0;
                }
            }
            
            var camera = mainCamera.GetBehaviour(_behaviourRegistry.GetIdByName(EngineBehavioursConstants.CAMERA));
            if (camera != null)
            {
                if (camera.GetProperty(EngineBehavioursConstants.CAMERA_BACKGROUND_COLOR).Value is Dictionary<string, SerializedProperty> backgroundColor)
                {
                    backgroundColor["r"].Value = 0.074;
                    backgroundColor["g"].Value = 0.090;
                    backgroundColor["b"].Value = 0.116;
                    backgroundColor["a"].Value = 1;
                }
            }
        }

        var pointLight = await _entityManagementService.CreateEntity("Point Light");
        if (pointLight != null)
        {
            _entityManagementService.AddBehaviour(pointLight, _behaviourRegistry.GetIdByName(EngineBehavioursConstants.POINT_LIGHT)!.Value);
        }
    }
}
