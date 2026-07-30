namespace ReiEditor.Mcp.Hosting;

public interface IReiMcpHost : IAsyncDisposable
{
    ReiMcpHostStatus Status { get; }
    Uri? Endpoint { get; }
    string? LastError { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
