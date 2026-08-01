using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal interface IMcpEditorSession
{
    ReiEditorState GetState();
    ReiEntityList ListEntities();
    ReiEntityDetails GetEntity(int entityId);
    ReiEntityMutationResult RenameEntity(int entityId, string newName);
    ReiBehaviourMutationResult AddBehaviour(int entityId, string behaviourName);
    ReiBehaviourPropertyMutationResult SetBehaviourProperty(int entityId, string behaviourName, string propertyName, object? value);
    Task<ReiMaterialPropertyMutationResult> SetMaterialPropertyAsync(string materialAssetId, string propertyName, object? value);
    Task<ReiProjectSaveResult> SaveProjectAsync();
    ReiOperationInfo StartAssetRefresh();
    ReiOperationInfo StartBuild(ReiBuildOptions options);
    ReiOperationInfo StartPlaymode();
    ReiOperationInfo StopPlaymode();
    ReiOperationInfo GetOperation(string operationId);
    ReiOperationInfo CancelOperation(string operationId);
    ReiLogList GetLogs(string? operationId, string minimumLevel, int limit);
    Task<ReiFrameCapture> CaptureFrameAsync(CancellationToken cancellationToken);
}
