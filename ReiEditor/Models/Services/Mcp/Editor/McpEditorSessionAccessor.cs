using System;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal sealed class McpEditorSessionAccessor : IMcpEditorSessionAccessor
{
    private sealed class SessionLease : IDisposable
    {
        private McpEditorSessionAccessor? _owner;
        private readonly IMcpEditorSession _session;

        public SessionLease(McpEditorSessionAccessor owner, IMcpEditorSession session)
        {
            _owner = owner;
            _session = session;
        }

        public void Dispose()
        {
            var owner = _owner;
            _owner = null;
            owner?.Detach(_session);
        }
    }

    private readonly object _sync = new();
    private IMcpEditorSession? _session;

    public IDisposable Attach(IMcpEditorSession session)
    {
        lock (_sync)
        {
            if (_session != null) throw new InvalidOperationException("An MCP editor session is already attached.");
            _session = session;
        }

        return new SessionLease(this, session);
    }

    public bool TryGetSession(out IMcpEditorSession? session)
    {
        lock (_sync)
        {
            session = _session;
            return session != null;
        }
    }

    private void Detach(IMcpEditorSession session)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_session, session)) _session = null;
        }
    }
}
