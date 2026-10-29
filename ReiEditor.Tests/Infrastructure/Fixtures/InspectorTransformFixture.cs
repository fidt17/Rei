using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;

namespace ReiEditor.Tests.Infrastructure.Fixtures;

internal sealed class InspectorTransformFixture : IDisposable
{
    private sealed class Registry(BehaviourAssetInfo transform, BehaviourAssetInfo rect) : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = new Dictionary<int, BehaviourAssetInfo>
        {
            [transform.BehaviourId] = transform, [rect.BehaviourId] = rect
        };
        public int? GetIdByName(string name) => name == EngineBehavioursConstants.TRANSFORM ? transform.BehaviourId
            : name == EngineBehavioursConstants.RECT_TRANSFORM ? rect.BehaviourId : null;
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => Behaviours.TryGetValue(id, out behaviour);
        public int AllocateBehaviourId() => throw new NotSupportedException();
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    // Same Transform ID as the shared native project fixture (PROBE_TRANSFORM).
    public const int TRANSFORM_ID = 7199;
    public const int RECT_TRANSFORM_ID = 7303;
    public GameEntity Entity { get; }
    public BehaviourComponent Transform { get; }
    public TestEngineRunner Runner { get; } = new();
    public Vector3PropertyViewModel ViewModel { get; }
    public EntityStateApplier StateApplier { get; }
    public TestLogger<EntitySyncService> Logger { get; } = new();
    private readonly EntitySyncService _sync;

    public InspectorTransformFixture(IEntityApi api, int entityId = 20)
    {
        Runner.Active.Value = true;
        Runner.EditorActive.Value = true;
        Runner.ActiveMode = EngineRunMode.EditorMode;
        Entity = new GameEntity(entityId, "Transform");
        var vectorData = Data(SerializedTypeEnum.Custom, "Vector3");
        var transformInfo = new BehaviourAssetInfo("rei", "Transform", TRANSFORM_ID, new ObjectFile<string>("", ""), new()
        {
            ["_position"] = vectorData, ["_rotation"] = vectorData, ["_scale"] = vectorData,
            ["_parent"] = Data(SerializedTypeEnum.Integer, "int"), ["_order"] = Data(SerializedTypeEnum.Integer, "int")
        }, [], "");
        var vector2Data = Data(SerializedTypeEnum.Custom, "Vector2");
        var rectInfo = new BehaviourAssetInfo("rei::ui", "RectTransform", RECT_TRANSFORM_ID, new ObjectFile<string>("", ""), new()
        {
            ["_anchorMin"] = vector2Data, ["_anchorMax"] = vector2Data, ["_pivot"] = vector2Data,
            ["_anchoredPosition"] = vector2Data, ["_sizeDelta"] = vector2Data
        }, [], "");
        var registry = new Registry(transformInfo, rectInfo);
        var objects = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
        objects.Replace([new SerializableObjectInfo("rei::math", "Vector3", false, new ObjectFile<string>("", ""), new()
        {
            ["x"] = Data(SerializedTypeEnum.Float, "float"), ["y"] = Data(SerializedTypeEnum.Float, "float"), ["z"] = Data(SerializedTypeEnum.Float, "float")
        }, ""), new SerializableObjectInfo("rei::math", "Vector2", false, new ObjectFile<string>("", ""), new()
        {
            ["x"] = Data(SerializedTypeEnum.Float, "float"), ["y"] = Data(SerializedTypeEnum.Float, "float")
        }, "")], []);
        var properties = new SerializedPropertiesService(objects, new TestLogger<SerializedPropertiesService>());
        var components = new BehaviourComponentsService(new TestLogger<BehaviourComponentsService>(), registry, properties);
        if (!components.AddComponent(Entity, TRANSFORM_ID)) throw new InvalidOperationException("Could not create test Transform.");
        Transform = Entity.GetBehaviour(TRANSFORM_ID)!;
        StateApplier = new EntityStateApplier(Logger, registry, components, properties);
        var importer = new TestAssetImporter();
        var behaviourSync = new BehaviourSyncService(importer, Runner, api, Logger, StateApplier);
        _sync = new EntitySyncService(importer, api, Runner, components, behaviourSync, StateApplier);
        ViewModel = new Vector3PropertyViewModel(Transform.GetProperty("_position"));
    }

    public void Apply(GetEntityDataResponse state) => StateApplier.Apply(Entity, state);
    public void Dispose() { ViewModel.Dispose(); _sync.Dispose(); }

    private static SerializableObjectInfo.SerializedPropertyData Data(SerializedTypeEnum type, string source) => new(type, source, null, SerializedTypeEnum.Invalid, null, null, type == SerializedTypeEnum.Custom ? null : "0", false);
}
