using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting;

/// <summary>Verifies nested component defaults, persisted property migration, collection updates and subscription ownership.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Assets")]
public sealed class BehaviourPropertySerializationTests
{
    /// <summary>Supplies explicit in-memory schemas while rejecting file discovery and metadata allocation.</summary>
    private sealed class TestDefinitions : IBehaviourRegistry, ISerializableObjectsRegistry
    {
        public Dictionary<int, BehaviourAssetInfo> Definitions { get; } = new();
        public Dictionary<string, SerializableObjectInfo> Objects { get; } = new();
        public Dictionary<string, SerializableEnum> Enums { get; } = new();
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours => Definitions;
        public int? GetIdByName(string name) => Definitions.Values.FirstOrDefault(x => x.ObjectName == name)?.BehaviourId;
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => Definitions.TryGetValue(id, out behaviour);
        public int AllocateBehaviourId() => throw new NotSupportedException();
        public Task RefreshBehaviours() => throw new NotSupportedException();
        public Task Refresh() => throw new NotSupportedException();
        public IEnumerable<SerializableObjectInfo> GetObjects() => Objects.Values;
        public SerializableObjectInfo? GetObject(string name) => Objects.GetValueOrDefault(SerializedTypeNameParser.GetBaseTypeName(name));
        public SerializableEnum? GetEnum(string name) => Enums.GetValueOrDefault(name);

        public void DefineObject(string name, Dictionary<string, SerializableObjectInfo.SerializedPropertyData> properties)
            => Objects[name] = new SerializableObjectInfo("game", name, false, new ObjectFile<string>("", name + ".h"), properties, name + ".h");

        public void DefineBehaviour(string name, Dictionary<string, SerializableObjectInfo.SerializedPropertyData> properties)
            => Definitions[1] = new BehaviourAssetInfo("game", name, 1, new ObjectFile<string>("", name + ".h"), properties, [], name + ".h");
    }

    /// <summary>Enum defaults accept numeric and qualified names, and an omitted default uses the first declared option.</summary>
    [Theory]
    [InlineData("42", 42)]
    [InlineData("game::State::Ready", 7)]
    [InlineData(null, 3)]
    [InlineData(" ", 3)]
    public void EnumDefaultsRespectDeclarationAndExplicitValue(string? defaultValue, int expected)
    {
        var definitions = new TestDefinitions();
        definitions.Enums["State"] = new SerializableEnum { EnumName = "State", Options = new() { ["Idle"] = 3, ["Ready"] = 7 } };
        definitions.DefineBehaviour("Mover", new() { ["State"] = Data(SerializedTypeEnum.Enum, "game::State", defaultValue) });
        var service = CreateService(definitions);
        var entity = new GameEntity(10, "Entity");

        Assert.True(service.AddComponent(entity, 1));

        Assert.Equal(expected, entity.GetBehaviour(1)!.GetProperty("State").Value);
    }

    /// <summary>Missing enum metadata logs an error and supplies zero so component creation can continue.</summary>
    [Fact]
    public void MissingEnumDefinitionLogsAndUsesZero()
    {
        var definitions = new TestDefinitions();
        definitions.DefineBehaviour("Mover", new() { ["State"] = Data(SerializedTypeEnum.Enum, "Missing") });
        var logger = new TestLogger<BehaviourComponentsService>();
        var service = new BehaviourComponentsService(logger, definitions, definitions);
        var entity = new GameEntity(10, "Entity");

        Assert.True(service.AddComponent(entity, 1));

        Assert.Equal(0, entity.GetBehaviour(1)!.GetProperty("State").Value);
        Assert.Contains("Missing", Assert.Single(logger.Entries).Message);
    }

    /// <summary>Nested defaults retain parent identity and repeated refresh does not duplicate leaf-change subscriptions.</summary>
    [Fact]
    public void NestedDefaultsRetainHierarchyAndSingleChangeSubscription()
    {
        var definitions = new TestDefinitions();
        definitions.DefineObject("Options", new() { ["Count"] = Data(SerializedTypeEnum.Integer, "int", "5") });
        definitions.DefineBehaviour("Mover", new() { ["Options"] = Data(SerializedTypeEnum.Custom, "game::Options") });
        var service = CreateService(definitions);
        var entity = new GameEntity(10, "Entity");
        Assert.True(service.AddComponent(entity, 1));
        var component = entity.GetBehaviour(1)!;
        var parent = component.GetProperty("Options");
        var child = Children(parent)["Count"];
        Assert.Same(parent, child.ParentProperty);
        Assert.Equal(5, child.Value);
        service.RefreshComponents(entity);
        service.RefreshComponents(entity);
        var changes = new List<EntityBehaviourPropertyChangeEventArgs>();
        service.BehaviourPropertyChangedEvent += changes.Add;

        child.Value = 9;

        var change = Assert.Single(changes);
        Assert.Same(child, change.Property);
    }

    /// <summary>Reserved Transform and MeshRenderer components receive scale and material defaults through real nested metadata.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EngineComponentDefaultsInitializeNestedValues(bool transform)
    {
        var definitions = new TestDefinitions();
        var name = transform ? EngineBehavioursConstants.TRANSFORM : EngineBehavioursConstants.MESH_RENDERER;
        var field = transform ? EngineBehavioursConstants.TRANSFORM_SCALE : EngineBehavioursConstants.MESH_RENDERER_MATERIAL;
        definitions.DefineObject("Nested", transform
            ? new() { ["x"] = Data(SerializedTypeEnum.Float, "float"), ["y"] = Data(SerializedTypeEnum.Float, "float"), ["z"] = Data(SerializedTypeEnum.Float, "float") }
            : new() { ["Id"] = Data(SerializedTypeEnum.String, "string") });
        definitions.DefineBehaviour(name, new() { [field] = Data(SerializedTypeEnum.Custom, "Nested") });
        var entity = new GameEntity(10, "Entity");

        Assert.True(CreateService(definitions).AddComponent(entity, 1));

        var nested = Children(entity.GetBehaviour(1)!.GetProperty(field));
        if (transform)
        {
            Assert.Equal(new[] { "x", "y", "z" }, nested.Keys);
            Assert.All(nested.Values, property => Assert.Equal(1, Convert.ToInt32(property.Value)));
        }
        else
        {
            Assert.Equal(EngineBehavioursConstants.DEFAULT_ENGINE_SIMPLE_LIT_MATERIAL_ASSET_ID, nested["Id"].Value);
        }
    }

    /// <summary>Persisted custom payloads accept raw and wrapped children, omit obsolete names and create newly declared defaults.</summary>
    [Fact]
    public void RefreshParsesRawAndWrappedChildrenAgainstCurrentSchema()
    {
        var definitions = new TestDefinitions();
        definitions.DefineObject("Options", new()
        {
            ["Raw"] = Data(SerializedTypeEnum.Integer, "int"),
            ["Wrapped"] = Data(SerializedTypeEnum.String, "string"),
            ["Added"] = Data(SerializedTypeEnum.Boolean, "bool", "true")
        });
        var property = new SerializedProperty("Options", SerializedTypeEnum.Custom, new JObject
        {
            ["Raw"] = 23, ["Wrapped"] = Wrapped(SerializedTypeEnum.String, "string", new JValue("kept")), ["Obsolete"] = 99
        }, "Options", null);

        RefreshProperty(definitions, property);

        var children = Children(property);
        Assert.Equal(new[] { "Raw", "Wrapped", "Added" }, children.Keys);
        Assert.Equal(23L, children["Raw"].Value);
        Assert.Equal("kept", children["Wrapped"].Value);
        Assert.Equal(true, children["Added"].Value);
        Assert.All(children.Values, child => Assert.Same(property, child.ParentProperty));
    }

    /// <summary>Persisted Vector2/Vector3 migrations preserve shared coordinates and default newly introduced coordinates.</summary>
    [Theory]
    [InlineData("Vector2", "Vector3")]
    [InlineData("Vector3", "Vector2")]
    public void RefreshMigratesCompatibleNestedVectors(string previousType, string currentType)
    {
        var definitions = new TestDefinitions();
        definitions.DefineObject("Vector2", new() { ["x"] = Data(SerializedTypeEnum.Float, "float"), ["y"] = Data(SerializedTypeEnum.Float, "float") });
        definitions.DefineObject("Vector3", new() { ["x"] = Data(SerializedTypeEnum.Float, "float"), ["y"] = Data(SerializedTypeEnum.Float, "float"), ["z"] = Data(SerializedTypeEnum.Float, "float", "6") });
        definitions.DefineObject("Options", new() { ["Position"] = Data(SerializedTypeEnum.Custom, currentType) });
        var property = new SerializedProperty("Options", SerializedTypeEnum.Custom, new JObject
        {
            ["Position"] = Wrapped(SerializedTypeEnum.Custom, previousType, new JObject { ["x"] = 12.5, ["y"] = -4.0, ["z"] = 99.0 })
        }, "Options", null);

        RefreshProperty(definitions, property);

        var position = Children(property)["Position"];
        Assert.Equal(currentType, position.SourceType);
        Assert.Same(property, position.ParentProperty);
        var coordinates = Children(position);
        Assert.Equal(12.5, Convert.ToDouble(coordinates["x"].Value));
        Assert.Equal(-4.0, Convert.ToDouble(coordinates["y"].Value));
        if (currentType == "Vector3") Assert.Equal(6.0, Convert.ToDouble(coordinates["z"].Value));
        else Assert.False(coordinates.ContainsKey("z"));
    }

    /// <summary>Collection loading infers scalar and enum item types from source names and preserves parent/index metadata.</summary>
    [Theory]
    [InlineData("i32", SerializedTypeEnum.Integer, "17")]
    [InlineData("u32", SerializedTypeEnum.Integer, "17")]
    [InlineData("std::string", SerializedTypeEnum.String, "\"hello\"")]
    [InlineData("bool", SerializedTypeEnum.Boolean, "true")]
    [InlineData("f32", SerializedTypeEnum.Float, "2.5")]
    [InlineData("double", SerializedTypeEnum.Float, "2.5")]
    [InlineData("game::State", SerializedTypeEnum.Enum, "7")]
    public void RefreshInfersCollectionItemTypes(string sourceType, SerializedTypeEnum expectedType, string json)
    {
        var definitions = new TestDefinitions();
        definitions.Enums["State"] = new SerializableEnum { EnumName = "State", Options = new() { ["Ready"] = 7 } };
        var property = Collection(new JArray(JToken.Parse(json)), sourceType);

        RefreshProperty(definitions, property);

        var item = Assert.Single(Items(property));
        Assert.Equal(expectedType, item.Type);
        Assert.Equal(sourceType, item.SourceType);
        Assert.Equal("[0]", item.Name);
        Assert.Same(property, item.ParentProperty);
        Assert.True(JToken.DeepEquals(JToken.Parse(json), JToken.FromObject(item.Value!)));
    }

    /// <summary>Nested collection loading builds independent indexed child lists with complete parent chains.</summary>
    [Fact]
    public void RefreshBuildsNestedCollections()
    {
        var property = Collection(JArray.Parse("[[1,2],[3]]"), "std::vector<int>");

        RefreshProperty(new TestDefinitions(), property);

        var outer = Items(property);
        Assert.Equal(2, outer.Count);
        Assert.All(outer, item => Assert.Equal(SerializedTypeEnum.Collection, item.Type));
        Assert.Equal(new object?[] { 1L, 2L }, Items(outer[0]).Select(x => x.Value));
        Assert.Equal(3L, Assert.Single(Items(outer[1])).Value);
        Assert.Same(outer[0], Items(outer[0])[1].ParentProperty);
        Assert.Same(property, outer[0].ParentProperty);
        Assert.Equal("[1]", Items(outer[0])[1].Name);
    }

    /// <summary>Custom collection elements are deserialized with schema defaults and remain editable after loading.</summary>
    [Fact]
    public void RefreshBuildsCustomCollectionItems()
    {
        var definitions = new TestDefinitions();
        definitions.DefineObject("Options", new() { ["Count"] = Data(SerializedTypeEnum.Integer, "int"), ["Enabled"] = Data(SerializedTypeEnum.Boolean, "bool", "true") });
        var property = Collection(JArray.Parse("[{\"Count\":9}]"), "Options");

        RefreshProperty(definitions, property);

        var item = Assert.Single(Items(property));
        Assert.Equal(SerializedTypeEnum.Custom, item.Type);
        Assert.Equal(9L, Children(item)["Count"].Value);
        Assert.Equal(true, Children(item)["Enabled"].Value);
        Assert.Same(item, Children(item)["Count"].ParentProperty);
    }

    /// <summary>Same-size collection updates preserve item identities; resize notifies once and detaches removed children.</summary>
    [Fact]
    public void ApplyCollectionReusesItemsAndPublishesStructuralChanges()
    {
        var service = CreateService(new TestDefinitions());
        var property = Collection(new List<SerializedProperty>(), "int");
        var changes = 0;
        property.ValueChangedEvent += _ => changes++;
        service.ApplySerializedValue(property, new JArray(2, 4));
        var list = Items(property);
        var first = list[0];
        var second = list[1];
        Assert.Equal(1, changes);

        service.ApplySerializedValue(property, new JArray(6, 8));
        Assert.Same(list, Items(property));
        Assert.Same(first, list[0]);
        Assert.Same(second, list[1]);
        Assert.Equal(new object?[] { 6, 8 }, list.Select(x => x.Value));
        Assert.Equal(1, changes);

        service.ApplySerializedValue(property, new JArray(10));
        Assert.Same(first, Assert.Single(list));
        Assert.Equal(10, first.Value);
        Assert.Equal(2, changes);
        second.Value = 99;
        Assert.Equal(2, changes);
        first.Value = 12;
        Assert.Equal(3, changes);
        service.ApplySerializedValue(property, new JArray());
        Assert.Empty(list);
        Assert.Equal(4, changes);
        first.Value = 15;
        Assert.Equal(4, changes);
    }

    /// <summary>Changed collection item types replace stale items while repairing index names and template metadata.</summary>
    [Fact]
    public void ApplyCollectionReplacesIncompatibleItems()
    {
        var service = CreateService(new TestDefinitions());
        var property = Collection(new List<SerializedProperty>(), "int");
        var obsolete = new SerializedProperty("wrong", SerializedTypeEnum.String, "old", "string", property);
        property.Value = new List<SerializedProperty> { obsolete };
        var changes = 0;
        property.ValueChangedEvent += _ => changes++;

        service.ApplySerializedValue(property, new JArray(13));

        var item = Assert.Single(Items(property));
        Assert.NotSame(obsolete, item);
        Assert.Equal(SerializedTypeEnum.Integer, item.Type);
        Assert.Equal("[0]", item.Name);
        Assert.Equal(13, item.Value);
        Assert.Same(property, item.ParentProperty);
        Assert.Equal(1, changes);
    }

    /// <summary>Applying nested arrays updates retained child collections and creates additional inner items.</summary>
    [Fact]
    public void ApplyNestedCollectionPreservesExistingParentChain()
    {
        var service = CreateService(new TestDefinitions());
        var property = Collection(new List<SerializedProperty>(), "std::vector<int>");
        service.ApplySerializedValue(property, JArray.Parse("[[1]]"));
        var inner = Assert.Single(Items(property));
        var existing = Assert.Single(Items(inner));

        service.ApplySerializedValue(property, JArray.Parse("[[5,7]]"));

        Assert.Same(inner, Assert.Single(Items(property)));
        Assert.Same(existing, Items(inner)[0]);
        Assert.Equal(new object?[] { 5L, 7L }, Items(inner).Select(x => x.Value));
        Assert.All(Items(inner), item => Assert.Same(inner, item.ParentProperty));
    }

    /// <summary>Custom payload updates preserve existing child objects and do not add fields absent from the current schema.</summary>
    [Fact]
    public void ApplyCustomValuePreservesDeclaredChildren()
    {
        var definitions = new TestDefinitions();
        definitions.DefineObject("Options", new() { ["Count"] = Data(SerializedTypeEnum.Integer, "int") });
        var property = Collection(JArray.Parse("[{\"Count\":1}]"), "Options");
        RefreshProperty(definitions, property);
        var item = Assert.Single(Items(property));
        var count = Children(item)["Count"];

        CreateService(definitions).ApplySerializedValue(property, JArray.Parse("[{\"Count\":8,\"Unknown\":2}]"));

        Assert.Same(item, Assert.Single(Items(property)));
        Assert.Same(count, Assert.Single(Children(item)).Value);
        Assert.Equal(8L, count.Value);
    }

    /// <summary>Applying a direct custom patch updates declared values while retaining unrelated children and their identities.</summary>
    [Fact]
    public void ApplyDirectCustomPatchPreservesUnmentionedFields()
    {
        var parent = new SerializedProperty("Options", SerializedTypeEnum.Custom, null, "Options", null);
        var count = new SerializedProperty("Count", SerializedTypeEnum.Integer, 2, "int", parent);
        var label = new SerializedProperty("Label", SerializedTypeEnum.String, "keep", "string", parent);
        parent.Value = new Dictionary<string, SerializedProperty> { ["Count"] = count, ["Label"] = label };
        var notifications = new List<object?>();
        parent.ValueChangedEvent += notifications.Add;

        CreateService(new TestDefinitions()).ApplySerializedValue(parent, JObject.Parse("{\"Count\":11,\"Unknown\":4}"));

        Assert.Equal(11L, count.Value);
        Assert.Equal("keep", label.Value);
        Assert.Same(count, Children(parent)["Count"]);
        Assert.Same(label, Children(parent)["Label"]);
        Assert.Equal(2, Children(parent).Count);
        Assert.NotEmpty(notifications);
        Assert.All(notifications, value => Assert.Same(parent.Value, value));
    }

    /// <summary>An incompatible persisted child source type resets to the new schema default instead of preserving stale payload.</summary>
    [Fact]
    public void RefreshResetsIncompatibleNestedSourceType()
    {
        var definitions = new TestDefinitions();
        definitions.DefineObject("Options", new() { ["Count"] = Data(SerializedTypeEnum.Integer, "int", "5") });
        var property = new SerializedProperty("Options", SerializedTypeEnum.Custom, new JObject
        {
            ["Count"] = Wrapped(SerializedTypeEnum.String, "string", new JValue("obsolete"))
        }, "Options", null);

        RefreshProperty(definitions, property);

        var child = Assert.Single(Children(property)).Value;
        Assert.Equal(SerializedTypeEnum.Integer, child.Type);
        Assert.Equal("int", child.SourceType);
        Assert.Equal(5, child.Value);
        Assert.Same(property, child.ParentProperty);
    }

    /// <summary>Deleting a component ends service notifications from its former properties.</summary>
    [Fact]
    public void DeletedComponentStopsPublishingPropertyChanges()
    {
        var definitions = new TestDefinitions();
        definitions.DefineBehaviour("Mover", new() { ["Count"] = Data(SerializedTypeEnum.Integer, "int") });
        var service = CreateService(definitions);
        var entity = new GameEntity(10, "Entity");
        Assert.True(service.AddComponent(entity, 1));
        var component = entity.GetBehaviour(1)!;
        var property = component.GetProperty("Count");
        var changes = new List<EntityBehaviourPropertyChangeEventArgs>();
        service.BehaviourPropertyChangedEvent += changes.Add;

        Assert.True(service.DeleteComponent(entity, component));
        property.Value = 3;

        Assert.Empty(entity.Behaviours);
        Assert.Empty(changes);
    }

    /// <summary>Explicit and schema-driven component removal detach nested properties while new components remain subscribed.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RemovedComponentDetachesNestedProperties(bool removeDefinition, bool collection)
    {
        var definitions = new TestDefinitions();
        definitions.DefineObject("Options", new() { ["Count"] = Data(SerializedTypeEnum.Integer, "int", "1") });
        definitions.DefineBehaviour("Mover", new() { ["Options"] = Data(collection ? SerializedTypeEnum.Collection : SerializedTypeEnum.Custom, collection ? "std::vector<Options>" : "Options") });
        var service = CreateService(definitions);
        var entity = new GameEntity(10, "Entity");
        Assert.True(service.AddComponent(entity, 1));
        var oldComponent = entity.GetBehaviour(1)!;
        var root = oldComponent.GetProperty("Options");
        if (collection)
        {
            service.ApplySerializedValue(root, JArray.Parse("[{\"Count\":1}]"));
            service.RefreshComponents(entity);
        }
        var leaf = Children(collection ? Items(root)[0] : root)["Count"];
        var changes = new List<EntityBehaviourPropertyChangeEventArgs>();
        service.BehaviourPropertyChangedEvent += changes.Add;
        if (removeDefinition)
        {
            definitions.Definitions.Clear();
            service.RefreshComponents(entity);
        }
        else
        {
            Assert.True(service.DeleteComponent(entity, oldComponent));
        }

        leaf.Value = 2;
        root.TriggerChangedEvent();
        Assert.Empty(entity.Behaviours);
        Assert.Empty(changes);

        definitions.DefineBehaviour("Mover", new() { ["Count"] = Data(SerializedTypeEnum.Integer, "int", "0") });
        Assert.True(service.AddComponent(entity, 1));
        var newComponent = entity.GetBehaviour(1)!;
        newComponent.GetProperty("Count").Value = 3;
        Assert.Same(newComponent, Assert.Single(changes).Component);
        Assert.NotSame(oldComponent, newComponent);
    }

    /// <summary>Schema removal and type replacement detach the former nested field and subscribe its replacement once.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefreshDetachesRemovedNestedField(bool replaceType)
    {
        var definitions = new TestDefinitions();
        definitions.DefineObject("Options", new() { ["Count"] = Data(SerializedTypeEnum.Integer, "int", "1") });
        definitions.DefineBehaviour("Mover", new() { ["Options"] = Data(SerializedTypeEnum.Custom, "Options") });
        var service = CreateService(definitions);
        var entity = new GameEntity(10, "Entity");
        Assert.True(service.AddComponent(entity, 1));
        var component = entity.GetBehaviour(1)!;
        var oldRoot = component.GetProperty("Options");
        var oldLeaf = Children(oldRoot)["Count"];
        var replacementName = replaceType ? "Options" : "Count";
        definitions.DefineBehaviour("Mover", new() { [replacementName] = Data(SerializedTypeEnum.Integer, "int", "0") });

        service.RefreshComponents(entity);
        service.RefreshComponents(entity);
        Assert.Equal(replacementName, Assert.Single(component.Properties).Key);
        var changes = new List<EntityBehaviourPropertyChangeEventArgs>();
        service.BehaviourPropertyChangedEvent += changes.Add;
        oldLeaf.Value = 4;
        oldRoot.TriggerChangedEvent();
        Assert.Empty(changes);

        var replacement = component.GetProperty(replacementName);
        replacement.Value = 5;
        Assert.Same(replacement, Assert.Single(changes).Property);
    }

    /// <summary>Deletion also detaches a previously subscribed child that was replaced before cleanup.</summary>
    [Fact]
    public void DeletionDetachesPreviouslyReplacedChildren()
    {
        var definitions = new TestDefinitions();
        definitions.DefineObject("Options", new() { ["Count"] = Data(SerializedTypeEnum.Integer, "int", "1") });
        definitions.DefineBehaviour("Mover", new() { ["Options"] = Data(SerializedTypeEnum.Custom, "Options") });
        var service = CreateService(definitions);
        var entity = new GameEntity(10, "Entity");
        Assert.True(service.AddComponent(entity, 1));
        var component = entity.GetBehaviour(1)!;
        var root = component.GetProperty("Options");
        var oldLeaf = Children(root)["Count"];
        root.Value = new Dictionary<string, SerializedProperty>();
        var changes = new List<EntityBehaviourPropertyChangeEventArgs>();
        service.BehaviourPropertyChangedEvent += changes.Add;

        Assert.True(service.DeleteComponent(entity, component));
        oldLeaf.Value = 8;

        Assert.Empty(changes);
    }

    private static BehaviourComponentsService CreateService(TestDefinitions definitions) => new(new TestLogger<BehaviourComponentsService>(), definitions, definitions);
    private static SerializableObjectInfo.SerializedPropertyData Data(SerializedTypeEnum type, string source, string? value = null)
        => new(type, source, null, SerializedTypeEnum.Invalid, null, null, value, false);
    private static Dictionary<string, SerializedProperty> Children(SerializedProperty property) => Assert.IsType<Dictionary<string, SerializedProperty>>(property.Value);
    private static List<SerializedProperty> Items(SerializedProperty property) => Assert.IsType<List<SerializedProperty>>(property.Value);
    private static SerializedProperty Collection(object value, string itemSource) => new("Items", SerializedTypeEnum.Collection, value, $"std::vector<{itemSource}>", null, itemSource);
    private static JObject Wrapped(SerializedTypeEnum type, string source, JToken value) => new() { ["Type"] = (int)type, ["SourceType"] = source, ["Value"] = value };

    private static void RefreshProperty(TestDefinitions definitions, SerializedProperty property)
    {
        definitions.DefineBehaviour("Mover", new() { [property.Name] = Data(property.Type, property.SourceType) });
        var entity = new GameEntity(10, "Entity");
        var component = new BehaviourComponent(1);
        component.AddProperty(property);
        entity.AddBehaviour(component);
        CreateService(definitions).RefreshComponents(entity);
    }
}
