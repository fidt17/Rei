using System;
using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Scripting;

public class BehaviourComponentsService : IBehaviourComponentsService
{
    public event Action<EntityBehaviourPropertyChangeEventArgs>? BehaviourPropertyChangedEvent;

    private readonly Dictionary<BehaviourComponent, Dictionary<SerializedProperty, Action<object?>>> _propertySubscriptions = new();
    
    private readonly ILogger<BehaviourComponentsService> _logger;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly ISerializedPropertiesService _serializedPropertiesService;

    public BehaviourComponentsService(
        ILogger<BehaviourComponentsService> logger,
        IBehaviourRegistry behaviourRegistry,
        ISerializedPropertiesService serializedPropertiesService)
    {
        _logger = logger;
        _behaviourRegistry = behaviourRegistry;
        _serializedPropertiesService = serializedPropertiesService;
    }

    public bool AddComponent(GameEntity e, int behaviourId)
    {
        if (!_behaviourRegistry.TryGetById(behaviourId, out var componentInfo))
        {
            _logger.LogException(new UnregisteredBehaviourException(behaviourId));
            return false;
        }
        
        if (e.HasComponent(behaviourId))
        {
            _logger.LogError($"{e} already has a component {componentInfo.BehaviourId}:{componentInfo.ObjectName}");
            return false;
        }

        foreach (var requiredComponentName in componentInfo.RequiredComponentNames)
        {
            var requiredBehaviourId = _behaviourRegistry.GetIdByName(requiredComponentName);
            if (requiredBehaviourId == null)
            {
                _logger.LogError($"Could not find required component {requiredComponentName} for {componentInfo.ObjectName}");
                continue;
            }

            if (e.HasComponent(requiredBehaviourId.Value)) continue;
            AddComponent(e, requiredBehaviourId.Value);
        }

        var component = new BehaviourComponent(behaviourId);
        foreach (var sp in componentInfo.SerializedProperties)
        {
            var p = _serializedPropertiesService.Create(sp.Key, sp.Value, null);
            component.AddProperty(p);
            SubscribeToPropertyChange(e, component, p);
        }
        
        SetupCustomBehaviourValues(component);
        
        e.AddBehaviour(component);
        
        return true;
    }

    public bool DeleteComponent(GameEntity e, BehaviourComponent component)
    {
        if (!e.HasBehaviour(component))
        {
            _logger.LogError($"Cannot delete component {component.Id} from {e}. Entity does not have one.");
            return false;
        }

        if (TryGetRequiringComponent(e, component.Id, out var requiringComponentName))
        {
            _logger.LogError($"Cannot delete component {component.Id} from {e}. {requiringComponentName} requires it.");
            return false;
        }

        UnsubscribeFromPropertyChanges(component);
        e.DeleteBehaviour(component);
        return true;
    }

    public bool TryGetRequiringComponent(GameEntity e, int requiredBehaviourId, out string requiringComponentName)
    {
        requiringComponentName = "";
        if (!_behaviourRegistry.TryGetById(requiredBehaviourId, out var requiredBehaviourInfo)) return false;

        foreach (var behaviour in e.Behaviours)
        {
            if (behaviour.Id == requiredBehaviourId) continue;
            if (!_behaviourRegistry.TryGetById(behaviour.Id, out var behaviourInfo)) continue;
            if (!behaviourInfo.RequiredComponentNames.Contains(requiredBehaviourInfo.ObjectName)) continue;

            requiringComponentName = behaviourInfo.ObjectName;
            return true;
        }

        return false;
    }

    public void RefreshComponents(GameEntity e)
    {
        foreach (var component in e.Behaviours.ToList())
        {
            UnsubscribeFromPropertyChanges(component);

            if (!_behaviourRegistry.TryGetById(component.Id, out var componentInfo))
            {
                e.DeleteBehaviour(component);
                continue;
            }

            try
            {
                foreach (var propertyName in component.Properties.Keys.ToList())
                {
                    if (!componentInfo.SerializedProperties.ContainsKey(propertyName)) component.RemoveProperty(propertyName);
                }

                foreach (var definition in componentInfo.SerializedProperties)
                {
                    var propertyData = definition.Value;
                    if (component.HasProperty(definition.Key))
                    {
                        var existing = component.GetProperty(definition.Key);
                        if (existing.Type != propertyData.Type || existing.SourceType != propertyData.SourceType)
                        {
                            component.RemoveProperty(definition.Key);
                        }
                    }

                    if (!component.HasProperty(definition.Key))
                    {
                        component.AddProperty(_serializedPropertiesService.Create(definition.Key, propertyData, null));
                        if (componentInfo.ObjectName == EngineBehavioursConstants.CAMERA && definition.Key == EngineBehavioursConstants.CAMERA_RENDERER_SETTINGS)
                            TrySetAssetRefId(component, definition.Key, SpecialAssetIds.DEFAULT_RENDERER_SETTINGS);
                    }

                    var property = component.GetProperty(definition.Key);
                    if (property.TemplateTypeName == null)
                    {
                        property.SetTemplateTypeName(propertyData.TemplateTypeName ?? SourceFilesUtility.GetTemplateTypeName(propertyData.SourceType));
                    }
                    _serializedPropertiesService.Refresh(property);
                }
            }
            finally
            {
                foreach (var property in component.Properties.Values)
                {
                    SubscribeToPropertyChange(e, component, property);
                }
            }
        }
    }

    private void SubscribeToPropertyChange(GameEntity entity, BehaviourComponent component, SerializedProperty property)
    {
        if (!_propertySubscriptions.TryGetValue(component, out var subscriptions))
        {
            subscriptions = new Dictionary<SerializedProperty, Action<object?>>();
            _propertySubscriptions.Add(component, subscriptions);
        }
        if (subscriptions.ContainsKey(property)) return;

        if (property.Value is Dictionary<string, SerializedProperty> children)
        {
            foreach (var nested in children.Values)
            {
                SubscribeToPropertyChange(entity, component, nested);
            }
            if (property.Type != SerializedTypeEnum.Collection) return;
        }
        else if (property.Value is List<SerializedProperty> items)
        {
            foreach (var nested in items)
            {
                SubscribeToPropertyChange(entity, component, nested);
            }
        }

        Action<object?> handler = _ => BehaviourPropertyChangedEvent?.Invoke(new EntityBehaviourPropertyChangeEventArgs(entity, component, property));
        subscriptions.Add(property, handler);
        property.ValueChangedEvent += handler;
    }

    private void UnsubscribeFromPropertyChanges(BehaviourComponent component)
    {
        if (!_propertySubscriptions.Remove(component, out var subscriptions)) return;

        foreach (var (property, handler) in subscriptions)
        {
            property.ValueChangedEvent -= handler;
        }
    }

    private void SetupCustomBehaviourValues(BehaviourComponent component)
    {
        if (component.Id == _behaviourRegistry.GetIdByName(EngineBehavioursConstants.CAMERA))
        {
            TrySetAssetRefId(component, EngineBehavioursConstants.CAMERA_RENDERER_SETTINGS, SpecialAssetIds.DEFAULT_RENDERER_SETTINGS);
            return;
        }

        if (component.Id == _behaviourRegistry.GetIdByName(EngineBehavioursConstants.TRANSFORM))
        {
            if (component.GetProperty(EngineBehavioursConstants.TRANSFORM_SCALE).Value is not Dictionary<string, SerializedProperty> scaleValue)
            {
                _logger.LogError("Could not find scale property on transform component");
                return;
            }
            
            scaleValue["x"].Value = 1;
            scaleValue["y"].Value = 1;
            scaleValue["z"].Value = 1;
            return;
        }

        if (component.Id == _behaviourRegistry.GetIdByName(EngineBehavioursConstants.MESH_RENDERER))
        {
            TrySetAssetRefId(
                component,
                EngineBehavioursConstants.MESH_RENDERER_MATERIAL,
                EngineBehavioursConstants.DEFAULT_ENGINE_SIMPLE_LIT_MATERIAL_ASSET_ID);
        }
    }

    private static void TrySetAssetRefId(BehaviourComponent component, string propertyName, string assetId)
    {
        if (!component.HasProperty(propertyName)) return;
        var property = component.GetProperty(propertyName);
        if (property.Value is not Dictionary<string, SerializedProperty> nestedProperties) return;
        if (!nestedProperties.TryGetValue(EngineBehavioursConstants.ASSET_REF_ID, out var idProperty)) return;

        idProperty.Value = assetId;
    }
}
