using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ReiEditor.Mcp.Configuration;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Mcp.Tools;

namespace ReiEditor.Mcp.Hosting;

public sealed class ReiMcpHost : IReiMcpHost
{
    private const string SERVER_NAME = "rei-editor";
    private const string SERVER_VERSION = "0.1.0";
    private const string SERVER_INSTRUCTIONS = "Inspect editor state before scene operations. Mutations affect current editor session; call rei_editor_save_project explicitly to persist them.";

    private readonly ReiMcpOptions _options;
    private readonly IReiEditorGateway _gateway;
    private WebApplication? _application;

    public ReiMcpHostStatus Status { get; private set; } = ReiMcpHostStatus.Created;
    public Uri? Endpoint { get; private set; }
    public string? LastError { get; private set; }

    public ReiMcpHost(ReiMcpOptions options, IReiEditorGateway gateway)
    {
        _options = options;
        _gateway = gateway;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Status == ReiMcpHostStatus.Running) return;
        if (Status is ReiMcpHostStatus.Starting or ReiMcpHostStatus.Stopping)
        {
            throw new InvalidOperationException($"Cannot start MCP host while status is {Status}.");
        }

        _options.Validate();
        LastError = null;

        if (!_options.Enabled)
        {
            Status = ReiMcpHostStatus.Disabled;
            return;
        }

        Status = ReiMcpHostStatus.Starting;
        WebApplication? application = null;

        try
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                Args = [],
                ApplicationName = typeof(ReiMcpHost).Assembly.FullName,
                EnvironmentName = Environments.Production
            });

            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://127.0.0.1:{_options.Port}");
            builder.Services.AddSingleton(_gateway);
            builder.Services
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new Implementation
                    {
                        Name = SERVER_NAME,
                        Version = SERVER_VERSION
                    };
                    options.ServerInstructions = SERVER_INSTRUCTIONS;
                })
                .WithHttpTransport(options => options.Stateless = true)
                .WithTools<ReiEditorMcpTools>();

            application = builder.Build();
            application.Use(ValidateLoopbackHost);
            application.MapGet(ReiMcpOptions.HEALTH_PATH, () => Results.Ok(new
            {
                status = "ok",
                server = SERVER_NAME,
                version = SERVER_VERSION
            }));
            application.MapMcp(ReiMcpOptions.MCP_PATH);

            await application.StartAsync(cancellationToken);

            _application = application;
            Endpoint = BuildEndpoint(application);
            Status = ReiMcpHostStatus.Running;
        }
        catch (Exception exception)
        {
            if (application != null) await application.DisposeAsync();
            Status = ReiMcpHostStatus.Faulted;
            LastError = exception.Message;
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_application == null)
        {
            if (Status != ReiMcpHostStatus.Disabled) Status = ReiMcpHostStatus.Stopped;
            return;
        }

        Status = ReiMcpHostStatus.Stopping;
        var application = _application;
        _application = null;

        try
        {
            await application.StopAsync(cancellationToken);
        }
        finally
        {
            await application.DisposeAsync();
            Endpoint = null;
            Status = ReiMcpHostStatus.Stopped;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private static async Task ValidateLoopbackHost(HttpContext context, RequestDelegate next)
    {
        var host = context.Request.Host.Host;
        if (!string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Invalid Host header.");
            return;
        }

        await next(context);
    }

    private static Uri BuildEndpoint(WebApplication application)
    {
        var address = application.Urls.FirstOrDefault() ?? throw new InvalidOperationException("MCP host did not publish an address.");
        return new UriBuilder(address) { Path = ReiMcpOptions.MCP_PATH }.Uri;
    }
}
