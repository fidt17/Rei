using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Mcp.Editor;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Mcp;

[Trait("Area", "Selection")]
public sealed class McpEntitySelectionServiceTests
{
    private sealed class Api : TestEngineApi { }
    private sealed class Native : IEngineNativeAccess
    {
        public bool Available { get; set; } = true;
        public int Calls { get; private set; }
        public bool TryInvoke(Action<IEngineApi> action)
        {
            Calls++;
            if (!Available) return false;
            action(new Api());
            return true;
        }
    }
    private sealed class Selectable(GameEntity entity) : IEntitySelectable
    {
        public GameEntity Entity { get; } = entity;
        public void Select() { }
        public void Deselect() { }
    }

    private readonly TestEntityApi _api = new();
    private readonly Native _native = new();
    private readonly TestSceneManagementService _scenes = new();
    private readonly SelectionService _selection;
    private readonly McpEntitySelectionService _service;

    public McpEntitySelectionServiceTests()
    {
        _selection = new(_api);
        var scene = new Scene("selection");
        _scenes.Scene.Value = scene;
        foreach (var id in new[] { 1, 2 })
        {
            var entity = new GameEntity(id, $"entity-{id}");
            scene.AddEntity(entity);
            _selection.RegisterSelectable(new Selectable(entity));
        }
        _service = new(_selection, _scenes, _api, _native);
    }

    [Fact]
    public void SourcesReadIndependentStateWithoutMutation()
    {
        _selection.Select(_scenes.Scene.Value!.GetById(1)!, sendToEngine: false);
        _api.OnGetSceneEntities = () => new() { Entities = new() { new() { Id = 2, IsSelected = true } } };

        Assert.Equal(new[] { 1 }, _service.Read("editor").SelectedIds);
        Assert.Equal(0, _native.Calls);
        Assert.Equal(new[] { 2 }, _service.Read("runtime").SelectedIds);
        Assert.Equal(new[] { 1 }, _service.Read("editor").SelectedIds);
        Assert.Empty(_api.Selections);
    }

    [Fact]
    public void UnavailableRuntimeNeverFallsBackToEditor()
    {
        _selection.Select(_scenes.Scene.Value!.GetById(1)!, sendToEngine: false);
        _native.Available = false;
        var state = _service.Read("runtime");
        Assert.Equal("engine_unavailable", state.Status);
        Assert.Null(state.SelectedIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedNativeReadHasDistinctStatus(bool throws)
    {
        _api.OnGetSceneEntities = () => throws ? throw new InvalidOperationException("read probe") : null;
        var state = _service.Read("runtime");
        Assert.Equal("read_failed", state.Status);
        Assert.Null(state.SelectedIds);
    }

    [Fact]
    public void SetUsesSharedSelectionServiceDeduplicatesAndClears()
    {
        var state = _service.Set(new[] { 2, 1, 2 });
        Assert.Equal(new[] { 1, 2 }, state.SelectedIds);
        Assert.Equal(2, ((IEntitySelectable)_selection.ActiveSelection.Value!).Entity.Id);
        Assert.Equal(new[] { 1, 2 }, Assert.Single(_api.Selections).Order());
        Assert.Empty(_service.Set(Array.Empty<int>()).SelectedIds!);
        Assert.Empty(_api.Selections[1]);
    }

    [Fact]
    public void InvalidSetLeavesSelectionAndNativeRequestsUntouched()
    {
        _selection.Select(_scenes.Scene.Value!.GetById(1)!, sendToEngine: false);
        var error = Assert.Throws<ReiMcpOperationException>(() => _service.Set(new[] { 2, 999 }));
        Assert.Equal("entity_not_found", error.Code);
        Assert.Equal(new[] { 1 }, _service.Read("editor").SelectedIds);
        Assert.Empty(_api.Selections);
    }

    [Fact]
    public void UnregisteredEntityAndInvalidSourceAreExplicitErrors()
    {
        var entity = _scenes.Scene.Value!.GetById(2)!;
        _selection.UnregisterSelectable(_selection.GetEntitySelectable(entity)!);
        Assert.Equal("entity_not_selectable", Assert.Throws<ReiMcpOperationException>(() => _service.Set(new[] { 2 })).Code);
        Assert.Equal("invalid_source", Assert.Throws<ReiMcpOperationException>(() => _service.Read("cache")).Code);
        Assert.Empty(_api.Selections);
    }
}
