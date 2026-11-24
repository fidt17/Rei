using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Mcp.Editor;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Mcp;

[Trait("Area", "Mcp")]
public sealed class McpEntityParentTests
{
    private sealed class Automation : IMcpEditorAutomationService
    {
        public string Status { get; set; } = "running";
        public ReiEngineInfo GetEngineInfo() => new(Status, "EditorMode");
        public ReiAutomationState GetState() => throw new NotSupportedException();
        public Task<ReiProjectSaveResult> SaveProjectAsync() => throw new NotSupportedException();
        public ReiOperationInfo StartAssetRefresh() => throw new NotSupportedException();
        public ReiOperationInfo StartOpenScene(Func<Task> openScene) => throw new NotSupportedException();
        public ReiOperationInfo StartBuild(ReiBuildOptions options) => throw new NotSupportedException();
        public ReiOperationInfo StartPlaymode() => throw new NotSupportedException();
        public ReiOperationInfo StopPlaymode() => throw new NotSupportedException();
        public ReiOperationInfo GetOperation(string operationId) => throw new NotSupportedException();
        public ReiOperationInfo CancelOperation(string operationId) => throw new NotSupportedException();
        public ReiLogList GetLogs(string? operationId, string minimumLevel, int limit) => throw new NotSupportedException();
        public Task<ReiFrameCapture> CaptureFrameAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private readonly Scene _scene = new("parent");
    private readonly TestSceneManagementService _scenes = new();
    private readonly TestEntityManagementService _entities = new() { OnSetParent = (_, _, _) => { } };
    private readonly Automation _automation = new();

    public McpEntityParentTests()
    {
        _scenes.Scene.Value = _scene;
        _scene.AddEntity(new GameEntity(1, "parent"));
        _scene.AddEntity(new GameEntity(2, "child"));
        _scene.MoveEntity(_scene.GetById(2)!, _scene.GetById(1), 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AcceptedRequestUsesSharedServiceWithoutClaimingRuntimeState(int parentId)
    {
        using var session = Create();
        var result = session.SetEntityParent(2, parentId, int.MaxValue);
        Assert.True(result.Accepted);
        Assert.Equal(int.MaxValue, result.Order);
        var call = Assert.Single(_entities.ParentCalls);
        Assert.Same(_scene.GetById(2), call.Entity);
        Assert.Equal(parentId == 0 ? null : _scene.GetById(parentId), call.Parent);
        Assert.Equal(int.MaxValue, call.Order);
    }

    [Theory]
    [InlineData(1, 1, 0, "invalid_parent")]
    [InlineData(1, 2, 0, "invalid_parent")]
    [InlineData(2, 0, -1, "invalid_order")]
    [InlineData(999, 0, 0, "entity_not_found")]
    [InlineData(2, 999, 0, "entity_not_found")]
    public void InvalidRequestDoesNotMutateScene(int entityId, int parentId, int order, string code)
    {
        using var session = Create();
        Assert.Equal(code, Assert.Throws<ReiMcpOperationException>(() => session.SetEntityParent(entityId, parentId, order)).Code);
        Assert.Empty(_entities.ParentCalls);
        Assert.Equal(1, _scene.GetById(2)!.Transform.Parent);
    }

    [Fact]
    public void StoppedEngineIsExplicitFailure()
    {
        _automation.Status = "stopped";
        using var session = Create();
        Assert.Equal("engine_unavailable", Assert.Throws<ReiMcpOperationException>(() => session.SetEntityParent(2, 0, 0)).Code);
        Assert.Empty(_entities.ParentCalls);
    }

    private McpEditorSessionRegistration Create() => new(new McpEditorSessionAccessor(), null!, _scenes, null!, null!, _entities, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, _automation, null!, null!, null!, null!, null!, null!, new EditorCommandService(), null!);
}
