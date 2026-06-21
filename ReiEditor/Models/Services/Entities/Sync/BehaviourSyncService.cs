using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Entities.Sync;

public class BehaviourSyncService
{
    private readonly IAssetImporter _assetImporter;
    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly ILogger<EntitySyncService> _logger;
    private readonly IEntityStateApplier _entityStateApplier;

    public BehaviourSyncService(
        IAssetImporter assetImporter,
        IEngineRunner engineRunner,
        IEntityApi entityApi,
        ILogger<EntitySyncService> logger,
        IEntityStateApplier entityStateApplier)
    {
        _assetImporter = assetImporter;
        _engineRunner = engineRunner;
        _entityApi = entityApi;
        _logger = logger;
        _entityStateApplier = entityStateApplier;
    }

    public void WriteChangedProperty(EntityBehaviourPropertyChangeEventArgs args)
    {
        if (!_engineRunner.IsActive.Value) return;
        if (_assetImporter.IsImporting.Value) return;
        if (_entityStateApplier.IsApplyingEngineState) return;

        try
        {
            var request = new SetEntityDataRequest
            {
                SceneId = args.Entity.Id,
                Behaviours = new List<Dictionary<string, object?>>()
            };

            var behaviourData = new Dictionary<string, object?> { { "REI_BEHAVIOUR_ID", args.Component.Id } };
            request.Behaviours.Add(behaviourData);

            var propertyToSync = GetPropertyRootForSync(args.Property);
            behaviourData.Add(propertyToSync.Name, SerializePropertyChange(propertyToSync));

            if (request.Behaviours.Count == 0) return;

            _entityApi.SetData(request);
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
        }
    }

    private static SerializedProperty GetPropertyRootForSync(SerializedProperty property)
    {
        var current = property;
        while (current.ParentProperty is { } parent && (parent.Type == SerializedTypeEnum.Custom || parent.Type == SerializedTypeEnum.Collection))
        {
            current = parent;
        }

        return current;
    }

    private static Dictionary<string, object?> SerializePropertyChange(SerializedProperty property)
    {
        if (property.Type == SerializedTypeEnum.Collection)
        {
            var serializedItems = new List<object?>();
            if (property.Value is List<SerializedProperty> collectionItems)
            {
                foreach (var item in collectionItems)
                {
                    serializedItems.Add(SerializePropertyValue(item));
                }
            }

            return new Dictionary<string, object?>
            {
                { "Value", serializedItems }
            };
        }

        if (property.Type != SerializedTypeEnum.Custom)
        {
            return new Dictionary<string, object?>
            {
                { "Value", property.Value }
            };
        }

        var serializedChildren = new Dictionary<string, object?>();
        if (property.Value is Dictionary<string, SerializedProperty> nestedProperties)
        {
            foreach (var nestedProperty in nestedProperties.Values)
            {
                serializedChildren[nestedProperty.Name] = SerializePropertyChange(nestedProperty);
            }
        }

        return new Dictionary<string, object?>
        {
            { "Value", serializedChildren }
        };
    }

    private static object? SerializePropertyValue(SerializedProperty property)
    {
        if (property.Type == SerializedTypeEnum.Collection)
        {
            var serializedItems = new List<object?>();
            if (property.Value is List<SerializedProperty> collectionItems)
            {
                foreach (var item in collectionItems)
                {
                    serializedItems.Add(SerializePropertyValue(item));
                }
            }

            return serializedItems;
        }

        if (property.Type != SerializedTypeEnum.Custom) return property.Value;

        var serializedChildren = new Dictionary<string, object?>();
        if (property.Value is Dictionary<string, SerializedProperty> nestedProperties)
        {
            foreach (var nestedProperty in nestedProperties.Values)
            {
                serializedChildren[nestedProperty.Name] = SerializePropertyChange(nestedProperty);
            }
        }

        return serializedChildren;
    }
}
