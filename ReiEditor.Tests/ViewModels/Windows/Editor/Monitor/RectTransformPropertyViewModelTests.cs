using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.RectTransform;
using ReiEditor.Tests.Infrastructure.Builders;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.RectTransform;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies full RectTransform editor state, serialized writes, preservation, runtime sync, and disposal.
/// </summary>
[Trait("Area", "PropertyEditors")]
public sealed class RectTransformPropertyViewModelTests
{
    /// <summary>
    /// Records runtime layout payloads while rejecting unrelated entity API calls.
    /// </summary>
    private sealed class TestRecordingEntityApi : IEntityApi
    {
        public List<SetEntityDataRequest> Requests { get; } = [];

        public GetSceneEntitiesResponse? GetSceneEntities() => throw new NotSupportedException();
        public GetEntityDataResponse? GetEntityData(int sceneEntityId) => throw new NotSupportedException();
        public InstantiateEntityResponse? CreateNewEntity(string name) => throw new NotSupportedException();
        public void DestroyEntity(int sceneEntityId) => throw new NotSupportedException();
        public void Rename(int sceneEntityId, string newName) => throw new NotSupportedException();
        public void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order) => throw new NotSupportedException();
        public void SetData(SetEntityDataRequest request) => Requests.Add(request);
        public InstantiateEntityResponse? InstantiateEntity(InstantiateEntityRequest request) => throw new NotSupportedException();
        public void AddBehaviour(int sceneEntityId, int behaviourId) => throw new NotSupportedException();
        public void DeleteBehaviour(int sceneEntityId, int behaviourId) => throw new NotSupportedException();
        public void SelectEntity(int sceneEntityId, bool resetCurrentSelection = true) => throw new NotSupportedException();
        public void SetEntitySelection(SetEntitySelectionRequest request) => throw new NotSupportedException();
        public void ResetEntitySelection() => throw new NotSupportedException();
    }

    /// <summary>
    /// Supplies deterministic parent size and pivot-preservation results.
    /// </summary>
    private sealed class TestRectTransformLayoutService : IRectTransformLayoutService
    {
        public RectTransformLayoutData? PreservedPivot { get; set; }
        public RectTransformVector2 ParentSize { get; set; } = new(500, 400);

        public RectTransformVector2 GetParentSize(GameEntity entity) => ParentSize;
        public bool TryGetRectTransform(GameEntity entity, out BehaviourComponent rectTransform) => throw new NotSupportedException();
        public bool TryReadLayout(BehaviourComponent rectTransform, out RectTransformLayoutData data) => throw new NotSupportedException();

        public bool TryPreserveRectForPivot(GameEntity entity, BehaviourComponent rectTransform, float pivotX, float pivotY, out RectTransformLayoutData preservedLayout)
        {
            preservedLayout = PreservedPivot ?? default;
            return PreservedPivot.HasValue;
        }

        public bool TryPreserveRectForParent(GameEntity entity, GameEntity? newParent, out RectTransformLayoutData preservedLayout) => throw new NotSupportedException();
        public void ApplyLayoutToEditor(BehaviourComponent rectTransform, RectTransformLayoutData layout) => throw new NotSupportedException();

        public Dictionary<string, object?> SerializeVector2(RectTransformVector2 value)
            => new() { ["x"] = value.X, ["y"] = value.Y };
    }

    /// <summary>
    /// Groups RectTransform editor and its observable collaborators.
    /// </summary>
    private sealed record TestFixture(
        GameEntity Entity,
        BehaviourComponent Component,
        TestEngineRunner Runner,
        TestRecordingEntityApi Api,
        TestRectTransformLayoutService Layout,
        RectTransformPropertyViewModel ViewModel);

    /// <summary>
    /// Fixed layout exposes preset labels, toggles raw anchors, and writes values without runtime calls while inactive.
    /// </summary>
    [Fact]
    public void FixedLayoutEditsSerializedStateWithoutInactiveRuntimeSync()
    {
        var fixture = TestCreateFixture();
        using var viewModel = fixture.ViewModel;

        Assert.Equal("Preset", viewModel.AnchorModeName);
        Assert.Equal("Middle Center", viewModel.AnchorPresetName);
        Assert.Equal("Pos X", viewModel.HorizontalPrimaryLabel);
        Assert.Equal(10f, viewModel.HorizontalPrimaryValue);

        viewModel.ToggleRawAnchorsCommand.Execute(null);
        viewModel.HorizontalPrimaryValue = 25f;

        Assert.True(viewModel.IsRawAnchorsVisible);
        Assert.Equal(25f, TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)["x"].Value);
        Assert.Empty(fixture.Api.Requests);
    }

    /// <summary>
    /// Active engine receives one complete RectTransform payload after serialized edit.
    /// </summary>
    [Fact]
    public void ActiveEngineReceivesCompleteLayoutPayload()
    {
        var fixture = TestCreateFixture();
        using var viewModel = fixture.ViewModel;
        fixture.Runner.Active.Value = true;

        viewModel.HorizontalSecondaryValue = 140f;

        var request = Assert.Single(fixture.Api.Requests);
        Assert.Equal(fixture.Entity.Id, request.SceneId);
        var behaviour = Assert.Single(request.Behaviours);
        Assert.Equal(fixture.Component.Id, behaviour[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
        Assert.Equal(6, behaviour.Count);
        TestAssertSerializedVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN, 0.5f, 0.5f);
        TestAssertSerializedVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX, 0.5f, 0.5f);
        TestAssertSerializedVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_PIVOT, 0.5f, 0.5f);
        TestAssertSerializedVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, 10f, 20f);
        TestAssertSerializedVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, 140f, 50f);
    }

    /// <summary>
    /// Pivot edit applies layout-service preservation result and keeps rectangle-dependent values together.
    /// </summary>
    [Fact]
    public void PivotEditAppliesPreservedLayout()
    {
        var preserved = new RectTransformLayoutData(new(0.5f, 0.5f), new(0.5f, 0.5f), new(1f, 0.25f), new(60f, 70f), new(100f, 50f));
        var fixture = TestCreateFixture();
        fixture.Layout.PreservedPivot = preserved;
        using var viewModel = fixture.ViewModel;

        viewModel.PivotX = 1f;

        Assert.Equal(1f, viewModel.PivotX);
        Assert.Equal(0.25f, viewModel.PivotY);
        Assert.Equal(60f, viewModel.HorizontalPrimaryValue);
        Assert.Equal(70f, viewModel.VerticalPrimaryValue);
    }

    /// <summary>
    /// Applying anchor preset preserves current rectangle and updates stretch labels and selection.
    /// </summary>
    [Fact]
    public void PresetCommandUpdatesAnchorsAndPresentation()
    {
        var fixture = TestCreateFixture();
        using var viewModel = fixture.ViewModel;
        var stretch = Assert.Single(viewModel.Presets, x => x.DisplayName == "Stretch Both");

        stretch.ApplyCommand.Execute(null);

        Assert.Equal(0f, viewModel.AnchorMinX);
        Assert.Equal(0f, viewModel.AnchorMinY);
        Assert.Equal(1f, viewModel.AnchorMaxX);
        Assert.Equal(1f, viewModel.AnchorMaxY);
        Assert.True(viewModel.IsStretchX);
        Assert.True(viewModel.IsStretchY);
        Assert.True(stretch.IsSelected);
        Assert.Equal("Left", viewModel.HorizontalPrimaryLabel);
        Assert.Equal("Top", viewModel.VerticalPrimaryLabel);
        Assert.Equal(210f, viewModel.HorizontalPrimaryValue);
        Assert.Equal(190f, viewModel.HorizontalSecondaryValue);
        Assert.Equal(155f, viewModel.VerticalPrimaryValue);
        Assert.Equal(195f, viewModel.VerticalSecondaryValue);
    }

    /// <summary>
    /// Disposed editor stops tracking nested vector changes.
    /// </summary>
    [Fact]
    public void DisposeUnsubscribesAllVectorBindings()
    {
        var fixture = TestCreateFixture();
        var viewModel = fixture.ViewModel;
        var anchorX = TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN)["x"];
        var notifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RectTransformPropertyViewModel.AnchorMinX)) notifications++;
        };

        viewModel.Dispose();
        anchorX.Value = 0.25f;

        Assert.Equal(0, notifications);
    }

    private static TestFixture TestCreateFixture()
    {
        var entity = new GameEntity(7, "UI");
        var component = SceneComponentBuilder.RectTransform(9, new RectTransformLayoutData(
            new(0.5f, 0.5f),
            new(0.5f, 0.5f),
            new(0.5f, 0.5f),
            new(10f, 20f),
            new(100f, 50f)));
        entity.AddBehaviour(component);
        var runner = new TestEngineRunner();
        var api = new TestRecordingEntityApi();
        var layout = new TestRectTransformLayoutService();
        return new TestFixture(entity, component, runner, api, layout, new RectTransformPropertyViewModel(entity, component, runner, api, layout));
    }

    private static Dictionary<string, SerializedProperty> TestVector(BehaviourComponent component, string propertyName)
        => Assert.IsType<Dictionary<string, SerializedProperty>>(component.GetProperty(propertyName).Value);

    private static void TestAssertSerializedVector(IReadOnlyDictionary<string, object?> behaviour, string propertyName, float expectedX, float expectedY)
    {
        var vector = Assert.IsType<Dictionary<string, object?>>(behaviour[propertyName]);
        Assert.Equal(2, vector.Count);
        Assert.Equal(expectedX, vector["x"]);
        Assert.Equal(expectedY, vector["y"]);
    }

}
