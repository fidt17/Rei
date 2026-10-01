namespace ReiEditor.Mcp.Contracts;

public interface IReiEditorGateway
{
    Task<System.Text.Json.JsonElement> GetProfilingSnapshotAsync(string source, string view, string? expectedSessionId, int limit, CancellationToken cancellationToken);
    Task<System.Text.Json.JsonElement> StartProfilingCaptureAsync(int frameCount, CancellationToken cancellationToken);
    Task<ReiEditorState> GetStateAsync(CancellationToken cancellationToken);
    Task<ReiEntityList> ListEntitiesAsync(CancellationToken cancellationToken);
    Task<ReiEntityDetails> GetEntityAsync(int entityId, CancellationToken cancellationToken);
    Task<ReiEntityMutationResult> RenameEntityAsync(int entityId, string newName, CancellationToken cancellationToken);
    Task<ReiBehaviourMutationResult> AddBehaviourAsync(int entityId, string behaviourName, CancellationToken cancellationToken);
    Task<ReiBehaviourPropertyMutationResult> SetBehaviourPropertyAsync(
        int entityId,
        string behaviourName,
        string propertyName,
        object? value,
        CancellationToken cancellationToken);
    Task<ReiMaterialPropertyMutationResult> SetMaterialPropertyAsync(
        string materialAssetId,
        string propertyName,
        object? value,
        CancellationToken cancellationToken);
    Task<ReiDataAssetTypeList> ListDataAssetTypesAsync(CancellationToken cancellationToken);
    Task<ReiDataAssetList> ListDataAssetsAsync(CancellationToken cancellationToken);
    Task<ReiDataAssetCreationResult> CreateDataAssetAsync(string typeName, string projectPath, CancellationToken cancellationToken);
    Task<ReiAssetSelection> SelectAssetAsync(string assetId, CancellationToken cancellationToken);
    Task<ReiAssetState> GetAssetStateAsync(string assetId, string source, CancellationToken cancellationToken);
    Task<ReiDataAssetDetails> GetDataAssetAsync(string assetId, CancellationToken cancellationToken);
    Task<ReiDataAssetPropertyMutationResult> SetDataAssetPropertyAsync(string assetId, string propertyName, object? value, CancellationToken cancellationToken);
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
