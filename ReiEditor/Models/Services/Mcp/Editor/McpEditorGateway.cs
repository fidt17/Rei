using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal sealed class McpEditorGateway : IReiEditorGateway
{
    private readonly IMcpEditorSessionAccessor _sessionAccessor;
    private readonly IEditorThreadDispatcher _dispatcher;

    public McpEditorGateway(IMcpEditorSessionAccessor sessionAccessor, IEditorThreadDispatcher dispatcher)
    {
        _sessionAccessor = sessionAccessor;
        _dispatcher = dispatcher;
    }

    public Task<ReiEditorState> GetStateAsync(CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() =>
        {
            return _sessionAccessor.TryGetSession(out var session)
                ? session!.GetState()
                : new ReiEditorState(ReiEditorStatus.PROJECT_MANAGEMENT, null, null, null, null);
        }, cancellationToken);
    }

    public Task<ReiEntityList> ListEntitiesAsync(CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().ListEntities(), cancellationToken);
    }

    public Task<ReiEntityDetails> GetEntityAsync(int entityId, CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().GetEntity(entityId), cancellationToken);
    }

    public Task<ReiEntityMutationResult> RenameEntityAsync(int entityId, string newName, CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().RenameEntity(entityId, newName), cancellationToken);
    }

    public Task<ReiProjectSaveResult> SaveProjectAsync(CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeTaskAsync(() => GetRequiredSession().SaveProjectAsync(), cancellationToken);
    }

    public Task<ReiOperationInfo> StartAssetRefreshAsync(CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().StartAssetRefresh(), cancellationToken);
    }

    public Task<ReiOperationInfo> StartBuildAsync(ReiBuildOptions options, CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().StartBuild(options), cancellationToken);
    }

    public Task<ReiOperationInfo> StartPlaymodeAsync(CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().StartPlaymode(), cancellationToken);
    }

    public Task<ReiOperationInfo> StopPlaymodeAsync(CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().StopPlaymode(), cancellationToken);
    }

    public Task<ReiOperationInfo> GetOperationAsync(string operationId, CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().GetOperation(operationId), cancellationToken);
    }

    public Task<ReiOperationInfo> CancelOperationAsync(string operationId, CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().CancelOperation(operationId), cancellationToken);
    }

    public Task<ReiLogList> GetLogsAsync(string? operationId, string minimumLevel, int limit, CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() => GetRequiredSession().GetLogs(operationId, minimumLevel, limit), cancellationToken);
    }

    public Task<ReiFrameCapture> CaptureFrameAsync(CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeTaskAsync(() => GetRequiredSession().CaptureFrameAsync(cancellationToken), cancellationToken);
    }

    private IMcpEditorSession GetRequiredSession()
    {
        if (_sessionAccessor.TryGetSession(out var session)) return session!;

        throw new ReiMcpOperationException("editor_unavailable", "No project editor session is active. Open a Rei project and wait until it finishes loading.");
    }
}
