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
                : new ReiEditorState(ReiEditorStatus.PROJECT_MANAGEMENT, null, null, null);
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

    private IMcpEditorSession GetRequiredSession()
    {
        if (_sessionAccessor.TryGetSession(out var session)) return session!;

        throw new ReiMcpOperationException("editor_unavailable", "No project editor session is active. Open a Rei project and wait until it finishes loading.");
    }
}
