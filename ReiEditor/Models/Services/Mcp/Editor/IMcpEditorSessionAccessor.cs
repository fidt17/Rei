using System;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal interface IMcpEditorSessionAccessor
{
    IDisposable Attach(IMcpEditorSession session);
    bool TryGetSession(out IMcpEditorSession? session);
}
