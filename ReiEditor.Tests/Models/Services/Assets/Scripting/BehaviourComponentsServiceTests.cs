using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting;

/// <summary>Verifies component requirements, defaults, refresh migration, deletion guards, and change events.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Assets")]
public sealed class BehaviourComponentsServiceTests
{
    /// <summary>Stores explicitly configured behaviours by ID and name.</summary>
    private sealed class TestBehaviourRegistry : IBehaviourRegistry
    {
        private readonly Dictionary<int, BehaviourAssetInfo> _behaviours = new();

        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours => _behaviours;

        /// <summary>Adds or replaces configured behaviour definition.</summary>
        public void Set(BehaviourAssetInfo behaviour) => _behaviours[behaviour.BehaviourId] = behaviour;

        /// <summary>Removes configured behaviour definition.</summary>
        public void Remove(int id) => _behaviours.Remove(id);

        /// <summary>Finds behaviour ID by object name.</summary>
        public int? GetIdByName(string name) => _behaviours.Values.FirstOrDefault(item => item.ObjectName == name)?.BehaviourId;

        /// <summary>Finds behaviour by ID.</summary>
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => _behaviours.TryGetValue(id, out behaviour);

        /// <summary>Allocation is outside component-service scenarios.</summary>
        public int AllocateBehaviourId() => throw new NotSupportedException();

        /// <summary>Refresh is outside component-service scenarios.</summary>
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    /// <summary>Stores explicitly configured object and enum definitions.</summary>
    private sealed class TestSerializableObjectsRegistry : ISerializableObjectsRegistry
    {
        public List<SerializableObjectInfo> Objects { get; } = new();
        public List<SerializableEnum> Enums { get; } = new();

        /// <summary>Returns configured object definitions.</summary>
        public IEnumerable<SerializableObjectInfo> GetObjects() => Objects;

        /// <summary>Completes without discovery.</summary>
        public Task Refresh() => Task.CompletedTask;

        /// <summary>Finds object definition by normalized source type.</summary>
        public SerializableObjectInfo? GetObject(string objectName)
        {
            var baseName = objectName.Split("::").Last().Split('<')[0];
            return Objects.Find(item => item.ObjectName == baseName);
        }

        /// <summary>Finds enum definition by name.</summary>
        public SerializableEnum? GetEnum(string enumName) => Enums.Find(item => item.EnumName == enumName);
    }

    /// <summary>Adding component recursively adds requirements, applies defaults, and publishes property changes.</summary>
    [Fact]
    public void AddsRequiredComponentsDefaultsAndChangeSubscriptions()
    {
        var behaviours = new TestBehaviourRegistry();
        var objects = new TestSerializableObjectsRegistry();
        objects.Enums.Add(new SerializableEnum
        {
            EnumName = "State",
            Options = new Dictionary<string, int> { ["Idle"] = 0, ["Running"] = 3 }
        });
        behaviours.Set(Behaviour(1, "BaseRequirement", new() { ["Count"] = Property(SerializedTypeEnum.Integer, "int", "4") }));
        behaviours.Set(Behaviour(2, "Mover", new() { ["State"] = Property(SerializedTypeEnum.Enum, "State", "Running") }, "BaseRequirement"));
        var service = CreateService(behaviours, objects);
        var entity = new GameEntity(9, "Entity");
        EntityBehaviourPropertyChangeEventArgs? change = null;
        service.BehaviourPropertyChangedEvent += args => change = args;

        Assert.True(service.AddComponent(entity, 2));

        Assert.Equal(4, entity.GetBehaviour(1)!.GetProperty("Count").Value);
        var state = entity.GetBehaviour(2)!.GetProperty("State");
        Assert.Equal(3, state.Value);
        state.Value = 0;
        Assert.NotNull(change);
        Assert.Same(state, change!.Property);
    }

    /// <summary>Unknown and duplicate component additions fail without modifying entity.</summary>
    [Fact]
    public void RejectsUnknownAndDuplicateComponents()
    {
        var behaviours = new TestBehaviourRegistry();
        behaviours.Set(Behaviour(1, "Mover", new()));
        var service = CreateService(behaviours, new TestSerializableObjectsRegistry());
        var entity = new GameEntity(1, "Entity");

        Assert.False(service.AddComponent(entity, 99));
        Assert.True(service.AddComponent(entity, 1));
        Assert.False(service.AddComponent(entity, 1));
        Assert.Single(entity.Behaviours);
    }

    /// <summary>Required component cannot be deleted until requiring component is removed.</summary>
    [Fact]
    public void PreventsDeletingRequiredComponent()
    {
        var behaviours = new TestBehaviourRegistry();
        behaviours.Set(Behaviour(1, "BaseRequirement", new()));
        behaviours.Set(Behaviour(2, "Mover", new(), "BaseRequirement"));
        var service = CreateService(behaviours, new TestSerializableObjectsRegistry());
        var entity = new GameEntity(1, "Entity");
        service.AddComponent(entity, 2);

        Assert.False(service.DeleteComponent(entity, entity.GetBehaviour(1)!));
        Assert.True(service.DeleteComponent(entity, entity.GetBehaviour(2)!));
        Assert.True(service.DeleteComponent(entity, entity.GetBehaviour(1)!));
        Assert.Empty(entity.Behaviours);
    }

    /// <summary>Refresh removes missing behaviours, drops deleted fields, preserves compatible values, and replaces changed types.</summary>
    [Fact]
    public void RefreshesComponentsAgainstCurrentDefinitions()
    {
        var behaviours = new TestBehaviourRegistry();
        behaviours.Set(Behaviour(1, "Mover", new()
        {
            ["Preserved"] = Property(SerializedTypeEnum.Integer, "int", "1"),
            ["Changed"] = Property(SerializedTypeEnum.Integer, "int", "2"),
            ["Removed"] = Property(SerializedTypeEnum.Boolean, "bool", "true")
        }));
        behaviours.Set(Behaviour(2, "DeletedBehaviour", new()));
        var service = CreateService(behaviours, new TestSerializableObjectsRegistry());
        var entity = new GameEntity(1, "Entity");
        service.AddComponent(entity, 1);
        service.AddComponent(entity, 2);
        entity.GetBehaviour(1)!.GetProperty("Preserved").Value = 8;

        behaviours.Set(Behaviour(1, "Mover", new()
        {
            ["Preserved"] = Property(SerializedTypeEnum.Integer, "int", "1"),
            ["Changed"] = Property(SerializedTypeEnum.String, "string", "reset"),
            ["Added"] = Property(SerializedTypeEnum.Boolean, "bool", "true")
        }));
        behaviours.Remove(2);

        service.RefreshComponents(entity);

        var component = Assert.Single(entity.Behaviours);
        Assert.Equal(8, component.GetProperty("Preserved").Value);
        Assert.Equal("reset", component.GetProperty("Changed").Value);
        Assert.Equal(true, component.GetProperty("Added").Value);
        Assert.False(component.HasProperty("Removed"));
    }

    /// <summary>Creates service with inspectable error log.</summary>
    private static BehaviourComponentsService CreateService(TestBehaviourRegistry behaviours, TestSerializableObjectsRegistry objects)
    {
        return new BehaviourComponentsService(new TestLogger<BehaviourComponentsService>(), behaviours, objects);
    }

    /// <summary>Creates behaviour metadata for component scenarios.</summary>
    private static BehaviourAssetInfo Behaviour(
        int id,
        string name,
        Dictionary<string, SerializableObjectInfo.SerializedPropertyData> properties,
        params string[] requirements)
    {
        return new BehaviourAssetInfo("game", name, id, new ObjectFile<string>("", $"{name}.h"), properties, requirements, $"{name}.h");
    }

    /// <summary>Creates scalar property metadata.</summary>
    private static SerializableObjectInfo.SerializedPropertyData Property(SerializedTypeEnum type, string sourceType, string? defaultValue)
    {
        return new SerializableObjectInfo.SerializedPropertyData(
            type,
            sourceType,
            null,
            SerializedTypeEnum.Invalid,
            null,
            null,
            defaultValue,
            false);
    }
}
