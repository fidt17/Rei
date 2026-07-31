using System.Threading;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal sealed class McpEditorOperationContext
{
    private readonly McpEditorOperationCoordinator _coordinator;
    private readonly string _operationId;

    public CancellationToken CancellationToken { get; }
    public bool HasErrors => _coordinator.HasErrors(_operationId);

    public McpEditorOperationContext(
        McpEditorOperationCoordinator coordinator,
        string operationId,
        CancellationToken cancellationToken)
    {
        _coordinator = coordinator;
        _operationId = operationId;
        CancellationToken = cancellationToken;
    }

    public void Report(double progress, string message)
    {
        _coordinator.Report(_operationId, progress, message);
    }
}
