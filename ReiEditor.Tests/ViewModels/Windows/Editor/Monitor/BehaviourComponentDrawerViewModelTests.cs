using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies behavior component drawer schema projection, custom fields, expansion, and required-component deletion guard.
/// </summary>
[Trait("Area", "Monitor")]
public sealed class BehaviourComponentDrawerViewModelTests
{
    /// <summary>
    /// Supplies one behavior schema to component drawer.
    /// </summary>
    private sealed class TestBehaviourRegistry(BehaviourAssetInfo info) : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = new Dictionary<int, BehaviourAssetInfo> { [info.BehaviourId] = info };

        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour)
            => Behaviours.TryGetValue(id, out behaviour);

        public int? GetIdByName(string name) => Behaviours.Values.FirstOrDefault(x => x.ObjectName == name)?.BehaviourId;
        public int AllocateBehaviourId() => throw new NotSupportedException();
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    /// <summary>
    /// Controls required-component guard exposed by delete option.
    /// </summary>
    private sealed class TestBehaviourComponentsService : IBehaviourComponentsService
    {
        public event Action<EntityBehaviourPropertyChangeEventArgs>? BehaviourPropertyChangedEvent;
        public bool IsRequired { get; set; }
        public string RequiringName { get; set; } = "Dependent";

        public bool AddComponent(GameEntity e, int behaviourId) => throw new NotSupportedException();
        public bool DeleteComponent(GameEntity e, BehaviourComponent component) => throw new NotSupportedException();

        public bool TryGetRequiringComponent(GameEntity e, int requiredBehaviourId, out string requiringComponentName)
        {
            requiringComponentName = RequiringName;
            return IsRequired;
        }

        public void ApplySerializedValue(SerializedProperty property, object? value) => throw new NotSupportedException();
        public void RefreshComponents(GameEntity e) => BehaviourPropertyChangedEvent?.Invoke(null!);
    }

    /// <summary>
    /// Records deletion requests from context menu.
    /// </summary>
    private sealed class TestEntityManagementService : IEntityManagementService
    {
        public List<(GameEntity Entity, int Id)> Deleted { get; } = [];
        public Task<GameEntity?> CreateEntity(string name, GameEntity? parent = null) => throw new NotSupportedException();
        public void RenameEntity(GameEntity e, string name) => throw new NotSupportedException();
        public void SetParent(GameEntity e, GameEntity? parent, int idx) => throw new NotSupportedException();
        public void AddBehaviour(GameEntity e, int behaviourId) => throw new NotSupportedException();
        public void DeleteBehaviour(GameEntity e, int behaviourId) => Deleted.Add((e, behaviourId));
        public int? InstantiateEntity(GameEntity sourceEntity, string? requestedName = null, bool includeChildren = true) => throw new NotSupportedException();
        public void DestroyEntity(GameEntity e) => throw new NotSupportedException();
    }

    /// <summary>
    /// Inserts one synthetic custom editor and owns selected serialized names.
    /// </summary>
    private sealed class TestRectTransformPropertiesProvider : IRectTransformCustomPropertiesProvider
    {
        public TestMarkerViewModel Marker { get; } = new();
        public IEnumerable<BaseViewModel> CreateProperties(GameEntity entity, BehaviourComponent component) => [Marker];
        public bool OwnsSerializedProperty(BehaviourComponent component, string propertyName) => propertyName == "owned";
    }

    /// <summary>
    /// Marks custom property position and disposal in projected drawer list.
    /// </summary>
    private sealed class TestMarkerViewModel : BaseViewModel
    {
        public bool IsDisposed { get; private set; }
        public override void Dispose() => IsDisposed = true;
    }

    /// <summary>
    /// Drawer projects visible non-owned schema fields after custom fields and disposes projected editors.
    /// </summary>
    [Fact]
    public void PropertiesRespectVisibilityOwnershipOrderAndDisposal()
    {
        var info = TestBehaviourInfo(includeMissing: false);
        var component = TestComponent(includeMissing: false);
        var custom = new TestRectTransformPropertiesProvider();
        var drawer = TestCreateDrawer(info, component, custom, new TestBehaviourComponentsService(), new TestEntityManagementService());

        Assert.Equal("Mover", drawer.Name);
        Assert.Same(custom.Marker, drawer.Properties[0]);
        Assert.IsType<IntegerPropertyViewModel>(drawer.Properties[1]);
        Assert.Equal(2, drawer.Properties.Count);
        Assert.True(drawer.Expanded.Value);

        drawer.SwitchExpandState();
        Assert.False(drawer.Expanded.Value);
        drawer.Dispose();
        Assert.True(custom.Marker.IsDisposed);
    }

    /// <summary>
    /// Delete option reports requiring component and invokes entity deletion after guard clears.
    /// </summary>
    [Fact]
    public void DeleteOptionReflectsRequiredComponentGuard()
    {
        var info = TestBehaviourInfo(includeMissing: false);
        var component = TestComponent(includeMissing: false);
        var requirements = new TestBehaviourComponentsService { IsRequired = true };
        var entities = new TestEntityManagementService();
        using var drawer = TestCreateDrawer(info, component, new TestRectTransformPropertiesProvider(), requirements, entities);
        var delete = Assert.Single(drawer.ContextMenu.Options, x => x.Text == "Delete Component");

        Assert.False(delete.IsEnabled);
        Assert.Equal("Dependent requires Mover", delete.ToolTip);
        requirements.IsRequired = false;
        Assert.True(delete.IsEnabled);
        Assert.Null(delete.ToolTip);

        delete.Command.Execute(null);
        var request = Assert.Single(entities.Deleted);
        Assert.Equal(component.Id, request.Id);
    }

    /// <summary>
    /// Schema entry missing from serialized component fails drawer construction.
    /// </summary>
    [Fact]
    public void MissingSerializedSchemaPropertyFailsConstruction()
    {
        var info = TestBehaviourInfo(includeMissing: true);
        var component = TestComponent(includeMissing: false);

        Assert.Throws<Exception>(() => TestCreateDrawer(
            info,
            component,
            new TestRectTransformPropertiesProvider(),
            new TestBehaviourComponentsService(),
            new TestEntityManagementService()));
    }

    private static BehaviourComponentDrawerViewModel TestCreateDrawer(
        BehaviourAssetInfo info,
        BehaviourComponent component,
        TestRectTransformPropertiesProvider custom,
        TestBehaviourComponentsService requirements,
        TestEntityManagementService entities)
        => new(
            new GameEntity(1, "Entity"),
            component,
            new TestBehaviourRegistry(info),
            entities,
            requirements,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            custom);

    private static BehaviourAssetInfo TestBehaviourInfo(bool includeMissing)
    {
        var properties = new Dictionary<string, SerializableObjectInfo.SerializedPropertyData>
        {
            ["speed"] = TestPropertyData(SerializedTypeEnum.Integer, hide: false),
            ["hidden"] = TestPropertyData(SerializedTypeEnum.Integer, hide: true),
            ["owned"] = TestPropertyData(SerializedTypeEnum.Integer, hide: false)
        };
        if (includeMissing)
        {
            properties["missing"] = TestPropertyData(SerializedTypeEnum.Integer, hide: false);
        }

        return new BehaviourAssetInfo("Game", "Mover", 8, new ObjectFile<string>("", "C:/Mover.h"), properties, [], "Mover.h");
    }

    private static SerializableObjectInfo.SerializedPropertyData TestPropertyData(SerializedTypeEnum type, bool hide)
        => new(type, "int", null, SerializedTypeEnum.Invalid, null, null, "0", hide);

    private static BehaviourComponent TestComponent(bool includeMissing)
    {
        var component = new BehaviourComponent(8);
        component.AddProperty(new SerializedProperty("speed", SerializedTypeEnum.Integer, 3, "int", null));
        component.AddProperty(new SerializedProperty("hidden", SerializedTypeEnum.Integer, 4, "int", null));
        component.AddProperty(new SerializedProperty("owned", SerializedTypeEnum.Integer, 5, "int", null));
        if (includeMissing)
        {
            component.AddProperty(new SerializedProperty("missing", SerializedTypeEnum.Integer, 6, "int", null));
        }
        return component;
    }
}
