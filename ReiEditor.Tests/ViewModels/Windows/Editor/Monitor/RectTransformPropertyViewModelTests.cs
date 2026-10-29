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
        public void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order, bool recordHierarchyChange = false) => throw new NotSupportedException();
        public void ReorderEntities(IReadOnlyList<int> entityIds, int insertionIndex, bool recordHierarchyChange = false) => throw new NotSupportedException();
        public void ReparentEntities(IReadOnlyList<int> entityIds, int parentEntityId, int insertionIndex, bool recordHierarchyChange = false) => throw new NotSupportedException();
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
    /// Active engine receives only SizeDelta after a width edit.
    /// </summary>
    [Fact]
    public void ActiveEngineReceivesSizeOnlyPayload()
    {
        var fixture = TestCreateFixture();
        using var viewModel = fixture.ViewModel;
        fixture.Runner.Active.Value = true;

        viewModel.HorizontalSecondaryValue = 140f;

        var request = Assert.Single(fixture.Api.Requests);
        Assert.Equal(fixture.Entity.Id, request.SceneId);
        var behaviour = Assert.Single(request.Behaviours);
        Assert.Equal(fixture.Component.Id, behaviour[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
        Assert.Equal(2, behaviour.Count);
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

    [Fact]
    public void ReplacementVectorChildrenAreReadAndEditedWithoutReselection()
    {
        var fixture = TestCreateFixture();
        using var vm = fixture.ViewModel;
        var pivot = fixture.Component.GetProperty(EngineBehavioursConstants.RECT_TRANSFORM_PIVOT);
        var original = Assert.IsType<Dictionary<string, SerializedProperty>>(pivot.Value);
        pivot.Value = new Dictionary<string, SerializedProperty>
        {
            ["x"] = new("x", ReiEditor.Models.Services.Assets.Scripting.Serialization.Types.SerializedTypeEnum.Float, 0.25f, "float", pivot),
            ["y"] = new("y", ReiEditor.Models.Services.Assets.Scripting.Serialization.Types.SerializedTypeEnum.Float, 0.75f, "float", pivot)
        };
        Assert.Equal(0.25f, vm.PivotX);
        Assert.Equal(0.75f, vm.PivotY);
        vm.PivotX = 0.4f;
        Assert.Equal(0.4f, Assert.IsType<Dictionary<string, SerializedProperty>>(pivot.Value)["x"].Value);
        Assert.Equal(0.5f, original["x"].Value);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    public void PositionWritesOnlyPositionAndRecordsOnlyForEditorWithFixedAnchors(bool editor, bool stretchX, bool stretchY, bool expectedRecord)
    {
        var fixture = TestCreateFixture();
        using var vm = fixture.ViewModel;
        if (stretchX) vm.AnchorMaxX = 1;
        if (stretchY) vm.AnchorMaxY = 1;
        fixture.Runner.Active.Value = true;
        fixture.Runner.EditorActive.Value = editor;

        if (stretchX) vm.VerticalPrimaryValue = 35;
        else vm.HorizontalPrimaryValue = 25;

        var request = Assert.Single(fixture.Api.Requests);
        Assert.Equal(expectedRecord, request.RecordTransformChange);
        var data = Assert.Single(request.Behaviours);
        Assert.Equal(2, data.Count);
        Assert.Contains(EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, data.Keys);
        Assert.Equal(fixture.Component.Id, data[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void ManualAnchorsRecordOneLayoutWithoutRecalculatingPositionOrSize(int field, bool editor)
    {
        var fixture = TestCreateFixture();
        using var vm = fixture.ViewModel;
        fixture.Runner.Active.Value = true;
        fixture.Runner.EditorActive.Value = editor;
        var value = field < 2 ? 0.375f : 0.625f;
        switch (field)
        {
            case 0: vm.AnchorMinX = value; break;
            case 1: vm.AnchorMinY = value; break;
            case 2: vm.AnchorMaxX = value; break;
            case 3: vm.AnchorMaxY = value; break;
        }
        var request = Assert.Single(fixture.Api.Requests);
        Assert.Equal(editor, request.RecordTransformChange);
        var data = Assert.Single(request.Behaviours);
        Assert.Equal(5, data.Count);
        TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN, field == 0 ? value : 0.5f, field == 1 ? value : 0.5f);
        TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX, field == 2 ? value : 0.5f, field == 3 ? value : 0.5f);
        TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, 10, 20);
        TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, 100, 50);
        Assert.Equal(0.5f, vm.PivotX);
        Assert.Equal(0.5f, vm.PivotY);
    }

    [Theory]
    [InlineData(true, false, false, true, true)]
    [InlineData(true, false, false, false, true)]
    [InlineData(false, false, false, true, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, true, true, false)]
    public void SizeWritesOnlySizeAndRecordsOnlyForEditorWithFixedAnchors(bool editor, bool stretchX, bool stretchY, bool horizontal, bool expectedRecord)
    {
        var fixture = TestCreateFixture();
        using var vm = fixture.ViewModel;
        if (stretchX) vm.AnchorMaxX = 1;
        if (stretchY) vm.AnchorMaxY = 1;
        fixture.Runner.Active.Value = true;
        fixture.Runner.EditorActive.Value = editor;

        if (horizontal) vm.HorizontalSecondaryValue = 140;
        else vm.VerticalSecondaryValue = 140;

        var request = Assert.Single(fixture.Api.Requests);
        Assert.Equal(expectedRecord, request.RecordTransformChange);
        var data = Assert.Single(request.Behaviours);
        Assert.Equal(2, data.Count);
        Assert.Equal(fixture.Component.Id, data[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
        TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, horizontal ? 140 : 100, horizontal ? 50 : 140);
        Assert.Equal(10f, TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)["x"].Value);
        Assert.Equal(20f, TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)["y"].Value);
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(3, true, false)]
    [InlineData(0, true, true)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    [InlineData(3, true, true)]
    [InlineData(0, false, true)]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(3, false, true)]
    public void OffsetWritesOneRectangleAndPreservesOtherEdges(int edge, bool editor, bool stretchBoth)
    {
        var fixture = TestCreateFixture();
        using var vm = fixture.ViewModel;
        if (edge < 2 || stretchBoth) { vm.AnchorMinX = 0; vm.AnchorMaxX = 1; }
        if (edge >= 2 || stretchBoth) { vm.AnchorMinY = 0; vm.AnchorMaxY = 1; }
        var before = new[] { vm.HorizontalPrimaryValue, vm.HorizontalSecondaryValue, vm.VerticalPrimaryValue, vm.VerticalSecondaryValue };
        fixture.Runner.Active.Value = true;
        fixture.Runner.EditorActive.Value = editor;
        switch (edge)
        {
            case 0: vm.HorizontalPrimaryValue = 25; break;
            case 1: vm.HorizontalSecondaryValue = 25; break;
            case 2: vm.VerticalPrimaryValue = 25; break;
            case 3: vm.VerticalSecondaryValue = 25; break;
        }
        var request = Assert.Single(fixture.Api.Requests);
        Assert.Equal(editor, request.RecordTransformChange);
        var data = Assert.Single(request.Behaviours);
        Assert.Equal(3, data.Count);
        Assert.Equal(fixture.Component.Id, data[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
        Assert.Contains(EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, data.Keys);
        Assert.Contains(EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, data.Keys);
        var after = new[] { vm.HorizontalPrimaryValue, vm.HorizontalSecondaryValue, vm.VerticalPrimaryValue, vm.VerticalSecondaryValue };
        for (var i = 0; i < 4; i++) Assert.InRange(MathF.Abs(after[i] - (i == edge ? 25 : before[i])), 0f, 0.0001f);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void PivotWritesOnePreservedLayoutAndRecordsOnlyInEditor(bool horizontal, bool editor)
    {
        var fixture = TestCreateFixture();
        using var vm = fixture.ViewModel;
        var pivot = horizontal ? new RectTransformVector2(0.25f, 0.5f) : new RectTransformVector2(0.5f, 0.25f);
        var position = horizontal ? new RectTransformVector2(-15, 20) : new RectTransformVector2(10, 7.5f);
        fixture.Layout.PreservedPivot = new RectTransformLayoutData(new(0.5f, 0.5f), new(0.5f, 0.5f), pivot, position, new(100, 50));
        fixture.Runner.Active.Value = true;
        fixture.Runner.EditorActive.Value = editor;
        if (horizontal) vm.PivotX = 0.25f;
        else vm.PivotY = 0.25f;
        var request = Assert.Single(fixture.Api.Requests);
        Assert.Equal(editor, request.RecordTransformChange);
        var data = Assert.Single(request.Behaviours);
        Assert.Equal(4, data.Count);
        TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_PIVOT, pivot.X, pivot.Y);
        TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, position.X, position.Y);
        TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, 100, 50);
        Assert.Equal(fixture.Component.Id, data[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PresetsWriteOneLayoutWithoutPivotAndPreserveBounds(bool editor)
    {
        var fixture = TestCreateFixture();
        using var vm = fixture.ViewModel;
        fixture.Runner.Active.Value = true;
        fixture.Runner.EditorActive.Value = editor;
        foreach (var preset in vm.Presets)
        {
            fixture.Api.Requests.Clear();
            preset.ApplyCommand.Execute(null);
            var request = Assert.Single(fixture.Api.Requests);
            Assert.Equal(editor, request.RecordTransformChange);
            var data = Assert.Single(request.Behaviours);
            Assert.Equal(5, data.Count);
            Assert.Equal(fixture.Component.Id, data[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
            TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN, preset.Preset.MinX, preset.Preset.MinY);
            TestAssertSerializedVector(data, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX, preset.Preset.MaxX, preset.Preset.MaxY);
            Assert.Contains(EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, data.Keys);
            Assert.Contains(EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, data.Keys);
            Assert.Equal(0.5f, vm.PivotX);
            Assert.Equal(0.5f, vm.PivotY);
            var position = TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION);
            var size = TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA);
            var rect = RectTransformLayoutCalculator.CalculateRect(fixture.Layout.ParentSize, vm.AnchorMinX, vm.AnchorMinY, vm.AnchorMaxX, vm.AnchorMaxY,
                vm.PivotX, vm.PivotY, Convert.ToSingle(position["x"].Value), Convert.ToSingle(position["y"].Value), Convert.ToSingle(size["x"].Value), Convert.ToSingle(size["y"].Value));
            Assert.Equal(new RectTransformRect(210, 195, 310, 245), rect);
            Assert.True(preset.IsSelected);
        }
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
