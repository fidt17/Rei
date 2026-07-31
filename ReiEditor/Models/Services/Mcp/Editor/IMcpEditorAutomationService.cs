using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal interface IMcpEditorAutomationService
{
    ReiAutomationState GetState();
    ReiEngineInfo GetEngineInfo();
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
