using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Entities;

public sealed class EntityDataWriterService : IEntityDataWriterService
{
    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;

    public EntityDataWriterService(IEngineRunner engineRunner, IEntityApi entityApi)
    {
        _engineRunner = engineRunner;
        _entityApi = entityApi;
    }

    public bool SetBehaviourProperty(GameEntity entity, int behaviourId, string propertyName, object? value)
    {
        if (_engineRunner.IsActive.Value)
        {
            return SetRuntimeProperty(entity.Id, behaviourId, propertyName, value);
        }

        return SetEditorProperty(entity, behaviourId, propertyName, value);
    }

    public bool SetBehaviourProperty(GameEntity entity, int behaviourId, string propertyName, IReadOnlyDictionary<string, object?> value)
    {
        if (_engineRunner.IsActive.Value)
        {
            return SetRuntimeProperty(entity.Id, behaviourId, propertyName, value);
        }

        return SetEditorProperty(entity, behaviourId, propertyName, value);
    }

    private bool SetRuntimeProperty(int entityId, int behaviourId, string propertyName, object? value)
    {
        var request = new SetEntityDataRequest
        {
            SceneId = entityId,
            Behaviours = new List<Dictionary<string, object?>>
            {
                new()
                {
                    { SetEntityDataRequest.REI_BEHAVIOUR_ID, behaviourId },
                    { propertyName, SerializeRuntimeValue(value) }
                }
            }
        };

        _entityApi.SetData(request);
        return true;
    }

    private static object SerializeRuntimeValue(object? value)
    {
        if (value is IReadOnlyDictionary<string, object?> nestedValue)
        {
            return new Dictionary<string, object?>
            {
                { "Value", nestedValue.ToDictionary(x => x.Key, x => SerializeRuntimeValue(x.Value)) }
            };
        }

        return new Dictionary<string, object?>
        {
            { "Value", value }
        };
    }

    private static bool SetEditorProperty(GameEntity entity, int behaviourId, string propertyName, object? value)
    {
        var behaviour = entity.GetBehaviour(behaviourId);
        if (behaviour == null || !behaviour.HasProperty(propertyName)) return false;

        var property = behaviour.GetProperty(propertyName);
        if (value is IReadOnlyDictionary<string, object?> nestedValue)
        {
            return ApplyNestedEditorValue(property, nestedValue);
        }

        property.Value = value;
        return true;
    }

    private static bool ApplyNestedEditorValue(SerializedProperty property, IReadOnlyDictionary<string, object?> value)
    {
        if (property.Value is not Dictionary<string, SerializedProperty> nestedProperties) return false;

        foreach (var (name, nestedValue) in value)
        {
            if (!nestedProperties.TryGetValue(name, out var nestedProperty)) return false;

            if (nestedValue is IReadOnlyDictionary<string, object?> nestedDictionary)
            {
                if (!ApplyNestedEditorValue(nestedProperty, nestedDictionary)) return false;
                continue;
            }

            nestedProperty.Value = nestedValue;
        }

        return true;
    }
}
