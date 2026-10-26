using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal interface IMcpEditorSession
{
    System.Text.Json.JsonElement GetProfilingSnapshot(string source, string view, string? expectedSessionId, int limit);
    System.Text.Json.JsonElement StartProfilingCapture(int frameCount);
    ReiSelectionState GetSelectionState(string source);
    ReiSelectionState SetEntitySelection(System.Collections.Generic.IReadOnlyList<int> entityIds);
    ReiEditorState GetState();
    ReiEditorCommandState GetCommandState();
    Task<ReiEditorCommandState> UndoAsync();
    Task<ReiEditorCommandState> RedoAsync();
    ReiEntityList ListEntities();
    ReiEntityDetails GetEntity(int entityId);
    ReiEntityParentResult SetEntityParent(int entityId, int parentId, int order);
    Task<ReiEntityMutationResult> RenameEntityAsync(int entityId, string newName);
    ReiBehaviourMutationResult AddBehaviour(int entityId, string behaviourName);
    ReiBehaviourPropertyMutationResult SetBehaviourProperty(int entityId, string behaviourName, string propertyName, object? value);
    Task<ReiMaterialPropertyMutationResult> SetMaterialPropertyAsync(string materialAssetId, string propertyName, object? value);
    ReiDataAssetTypeList ListDataAssetTypes();
    ReiDataAssetList ListDataAssets();
    Task<ReiDataAssetCreationResult> CreateDataAssetAsync(string typeName, string projectPath);
    ReiAssetSelection SelectAsset(string assetId);
    Task<ReiAssetState> GetAssetStateAsync(string assetId, string source);
    Task<ReiDataAssetDetails> GetDataAssetAsync(string assetId);
    Task<ReiDataAssetPropertyMutationResult> SetDataAssetPropertyAsync(string assetId, string propertyName, object? value);
    Task<ReiProjectSaveResult> SaveProjectAsync();
    ReiOperationInfo StartAssetRefresh();
    ReiOperationInfo OpenScene(string scene);
    ReiOperationInfo StartBuild(ReiBuildOptions options);
    ReiOperationInfo StartPlaymode();
    ReiOperationInfo StopPlaymode();
    ReiOperationInfo GetOperation(string operationId);
    ReiOperationInfo CancelOperation(string operationId);
    ReiLogList GetLogs(string? operationId, string minimumLevel, int limit);
    Task<ReiFrameCapture> CaptureFrameAsync(CancellationToken cancellationToken);
}
