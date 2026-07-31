namespace ReiEditor.Mcp.Contracts;

public interface IReiEditorGateway
{
    Task<ReiEditorState> GetStateAsync(CancellationToken cancellationToken);
    Task<ReiEntityList> ListEntitiesAsync(CancellationToken cancellationToken);
    Task<ReiEntityDetails> GetEntityAsync(int entityId, CancellationToken cancellationToken);
    Task<ReiEntityMutationResult> RenameEntityAsync(int entityId, string newName, CancellationToken cancellationToken);
    Task<ReiProjectSaveResult> SaveProjectAsync(CancellationToken cancellationToken);
    Task<ReiOperationInfo> StartAssetRefreshAsync(CancellationToken cancellationToken);
    Task<ReiOperationInfo> StartBuildAsync(ReiBuildOptions options, CancellationToken cancellationToken);
    Task<ReiOperationInfo> StartPlaymodeAsync(CancellationToken cancellationToken);
    Task<ReiOperationInfo> StopPlaymodeAsync(CancellationToken cancellationToken);
    Task<ReiOperationInfo> GetOperationAsync(string operationId, CancellationToken cancellationToken);
    Task<ReiOperationInfo> CancelOperationAsync(string operationId, CancellationToken cancellationToken);
    Task<ReiLogList> GetLogsAsync(string? operationId, string minimumLevel, int limit, CancellationToken cancellationToken);
    Task<ReiFrameCapture> CaptureFrameAsync(CancellationToken cancellationToken);
}
