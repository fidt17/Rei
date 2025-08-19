using System;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.Assets.Scripting;

public class EntityBehaviourPropertyChangeEventArgs
{
    public GameEntity Entity { get; set; }
    public BehaviourComponent Component { get; set; }
    public SerializedProperty Property { get; set; }
    
    public EntityBehaviourPropertyChangeEventArgs(GameEntity entity, BehaviourComponent component, SerializedProperty property)
    {
        Entity = entity;
        Component = component;
        Property = property;
    }
}

public interface IBehaviourComponentsService
{
    event Action<EntityBehaviourPropertyChangeEventArgs>? BehaviourPropertyChangedEvent;
    
    bool AddComponent(GameEntity e, int behaviourId);
    bool DeleteComponent(GameEntity e, BehaviourComponent component);

    void RefreshComponents(GameEntity e);
}