using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Provides explicitly configured component operations for scene loading and synchronization.</summary>
internal sealed class TestBehaviourComponentsService : IBehaviourComponentsService
{
    public event Action<EntityBehaviourPropertyChangeEventArgs>? BehaviourPropertyChangedEvent;
    public Action<GameEntity>? OnRefresh { get; set; }
    public Func<GameEntity, int, bool>? OnAdd { get; set; }
    public Func<GameEntity, BehaviourComponent, bool>? OnDelete { get; set; }
    public Action<SerializedProperty, object?>? OnApply { get; set; }
    public void Publish(EntityBehaviourPropertyChangeEventArgs args) => BehaviourPropertyChangedEvent?.Invoke(args);
    public bool AddComponent(GameEntity e, int behaviourId) => (OnAdd ?? throw new NotSupportedException())(e, behaviourId);
    public bool DeleteComponent(GameEntity e, BehaviourComponent component) => (OnDelete ?? throw new NotSupportedException())(e, component);
    public void ApplySerializedValue(SerializedProperty property, object? value) => (OnApply ?? throw new NotSupportedException())(property, value);
    public void RefreshComponents(GameEntity e) => (OnRefresh ?? throw new NotSupportedException())(e);
    public bool TryGetRequiringComponent(GameEntity e, int requiredBehaviourId, out string requiringComponentName) => throw new NotSupportedException();
}
