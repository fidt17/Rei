using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using System.Diagnostics.CodeAnalysis;

namespace ReiEditor.Tests.Models.Services.Entities.Sync;

/// <summary>
/// Verifies engine entity state application, component reconciliation, and hierarchy updates.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "EntitySync")]
public sealed class EntityStateApplierTests
{
    private sealed class TestBehaviourRegistry(IReadOnlyDictionary<string, int> ids) : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = new Dictionary<int, BehaviourAssetInfo>();
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) { behaviour = null; return false; }
        public int? GetIdByName(string name) => ids.TryGetValue(name, out var id) ? id : null;
        public int AllocateBehaviourId() => throw new NotSupportedException();
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    private sealed class TestBehaviourComponentsService : IBehaviourComponentsService
    {
        public event Action<EntityBehaviourPropertyChangeEventArgs>? BehaviourPropertyChangedEvent
        {
            add => throw new NotSupportedException();
            remove => throw new NotSupportedException();
        }
        public string? ThrowForProperty { get; init; }
        public List<int> AddedIds { get; } = new();
        public List<int> DeletedIds { get; } = new();

        public bool AddComponent(GameEntity entity, int behaviourId)
        {
            AddedIds.Add(behaviourId);
            var component = new BehaviourComponent(behaviourId);
            if (behaviourId == 1)
            {
                component.AddProperty(Integer(EngineBehavioursConstants.TRANSFORM_PARENT, 0));
                component.AddProperty(Integer(EngineBehavioursConstants.TRANSFORM_ORDER, 0));
            }
            else
            {
                component.AddProperty(Integer("first", 0));
                component.AddProperty(Integer("second", 0));
            }
            entity.AddBehaviour(component);
            return true;
        }

        public bool DeleteComponent(GameEntity entity, BehaviourComponent component)
        {
            DeletedIds.Add(component.Id);
            entity.DeleteBehaviour(component);
            return true;
        }

        public bool TryGetRequiringComponent(GameEntity entity, int requiredBehaviourId, out string requiringComponentName)
        {
            requiringComponentName = "";
            return false;
        }

        public void ApplySerializedValue(SerializedProperty property, object? value)
        {
            if (property.Name == ThrowForProperty) throw new InvalidOperationException("property failed");
            property.Value = value;
        }

        public void RefreshComponents(GameEntity entity) => throw new NotSupportedException();

        private static SerializedProperty Integer(string name, int value)
            => new(name, SerializedTypeEnum.Integer, value, "int", null);
    }

    /// <summary>
    /// Applying a complete state adds missing components, updates fields, removes absent components, and reports transform changes.
    /// </summary>
    [Fact]
    public void CompleteStateReconcilesComponentsAndTransform()
    {
        var entity = new GameEntity(8, "Old");
        entity.AddBehaviour(new BehaviourComponent(99));
        var components = new TestBehaviourComponentsService();
        var applier = CreateApplier(components);
        var state = new GetEntityDataResponse
        {
            Name = "New",
            Behaviours =
            {
                new Dictionary<string, object>
                {
                    ["REI_TYPE"] = EngineBehavioursConstants.TRANSFORM,
                    [EngineBehavioursConstants.TRANSFORM_PARENT] = 4,
                    [EngineBehavioursConstants.TRANSFORM_ORDER] = 2
                },
                new Dictionary<string, object> { ["REI_TYPE"] = "Mover", ["first"] = 7 }
            }
        };

        var hierarchyChanged = applier.Apply(entity, state);

        Assert.True(hierarchyChanged);
        Assert.Equal("New", entity.Name);
        Assert.Equal(4, entity.Transform.Parent);
        Assert.Equal(2, entity.Transform.Order);
        Assert.Equal(new[] { 1, 2 }, components.AddedIds);
        Assert.Equal(new[] { 99 }, components.DeletedIds);
        Assert.Equal(7, entity.GetBehaviour(2)!.GetProperty("first").Value);
        Assert.False(applier.IsApplyingEngineState);
    }

    /// <summary>
    /// Unknown behaviour types are logged and preserve existing components because state resolution is incomplete.
    /// </summary>
    [Fact]
    public void UnknownBehaviourPreservesExistingComponents()
    {
        var entity = new GameEntity(8, "Entity");
        entity.AddBehaviour(new BehaviourComponent(99));
        var components = new TestBehaviourComponentsService();
        var logger = new TestLogger<EntitySyncService>();
        var applier = CreateApplier(components, logger);
        var state = new GetEntityDataResponse
        {
            Behaviours = { new Dictionary<string, object> { ["REI_TYPE"] = "Unknown" } }
        };

        var hierarchyChanged = applier.Apply(entity, state);

        Assert.False(hierarchyChanged);
        Assert.NotNull(entity.GetBehaviour(99));
        Assert.Empty(components.DeletedIds);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("Unknown", StringComparison.Ordinal));
    }

    /// <summary>
    /// One property conversion failure is logged while remaining fields continue to apply.
    /// </summary>
    [Fact]
    public void PropertyFailureDoesNotStopRemainingFields()
    {
        var entity = new GameEntity(8, "Entity");
        var components = new TestBehaviourComponentsService { ThrowForProperty = "first" };
        var logger = new TestLogger<EntitySyncService>();
        var applier = CreateApplier(components, logger);
        var state = new GetEntityDataResponse
        {
            Behaviours =
            {
                new Dictionary<string, object>
                {
                    ["REI_TYPE"] = "Mover",
                    ["first"] = 7,
                    ["second"] = 9
                }
            }
        };

        applier.Apply(entity, state);

        var component = entity.GetBehaviour(2)!;
        Assert.Equal(0, component.GetProperty("first").Value);
        Assert.Equal(9, component.GetProperty("second").Value);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("property failed", StringComparison.Ordinal));
    }

    /// <summary>
    /// A partial transform pair leaves hierarchy metadata unchanged and reports no refresh.
    /// </summary>
    [Fact]
    public void PartialTransformPairDoesNotChangeHierarchy()
    {
        var entity = new GameEntity(8, "Entity");
        entity.Transform.SetParent(3);
        entity.Transform.SetOrder(4);
        var applier = CreateApplier(new TestBehaviourComponentsService());
        var state = new GetEntityDataResponse
        {
            Behaviours =
            {
                new Dictionary<string, object>
                {
                    ["REI_TYPE"] = EngineBehavioursConstants.TRANSFORM,
                    [EngineBehavioursConstants.TRANSFORM_PARENT] = 9
                }
            }
        };

        var hierarchyChanged = applier.Apply(entity, state);

        Assert.False(hierarchyChanged);
        Assert.Equal(3, entity.Transform.Parent);
        Assert.Equal(4, entity.Transform.Order);
    }

    /// <summary>
    /// Unexpected outer failures always clear the applying-state guard.
    /// </summary>
    [Fact]
    public void OuterFailureClearsApplyingState()
    {
        var applier = CreateApplier(new TestBehaviourComponentsService());
        var state = new GetEntityDataResponse { Behaviours = null! };

        Assert.Throws<NullReferenceException>(() => applier.Apply(new GameEntity(1, "Entity"), state));
        Assert.False(applier.IsApplyingEngineState);
    }

    private static EntityStateApplier CreateApplier(TestBehaviourComponentsService components, TestLogger<EntitySyncService>? logger = null)
        => new(logger ?? new TestLogger<EntitySyncService>(), new TestBehaviourRegistry(new Dictionary<string, int>
        {
            [EngineBehavioursConstants.TRANSFORM] = 1,
            ["Mover"] = 2
        }), components);
}
