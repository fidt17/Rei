using System;
using System.Threading.Tasks;
using ReiEditor.Mcp.Configuration;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Mcp.Hosting;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Mcp.Hosting;

public sealed class McpHostLifecycleService : IMcpHostLifecycleService, IAsyncDisposable
{
    private readonly IReiEditorGateway _gateway;
    private readonly ILogger<McpHostLifecycleService> _logger;
    private IReiMcpHost? _host;

    public McpHostLifecycleService(IReiEditorGateway gateway, ILogger<McpHostLifecycleService> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        if (_host != null) return;

        try
        {
            var options = ReiMcpOptions.FromEnvironment();
            _host = new ReiMcpHost(options, _gateway);
            await _host.StartAsync();

            if (_host.Status == ReiMcpHostStatus.Disabled)
            {
                _logger.LogWarning("MCP server disabled by REI_MCP_ENABLED.");
                return;
            }

            _logger.Log($"MCP server listening at {_host.Endpoint}");
        }
        catch (Exception exception)
        {
            _logger.LogError("MCP server failed to start. ReiEditor will continue without MCP.");
            _logger.LogException(exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_host == null) return;

        await _host.DisposeAsync();
        _host = null;
    }
}
