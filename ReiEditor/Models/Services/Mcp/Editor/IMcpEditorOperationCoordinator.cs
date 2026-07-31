using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal interface IMcpEditorOperationCoordinator
{
    ReiOperationInfo? GetActiveOperation();
    ReiOperationInfo Start(string kind, Func<McpEditorOperationContext, Task<string>> operation);
    ReiOperationInfo Get(string operationId);
    ReiOperationInfo Cancel(string operationId);
    IReadOnlyList<ReiLogEntry> GetLogs(string operationId);
}
