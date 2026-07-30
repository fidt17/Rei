using System.Net;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ReiEditor.Mcp.Configuration;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Mcp.Hosting;

namespace ReiEditor.Mcp.Tests.Hosting;

public sealed class ReiMcpHostIntegrationTests
{
    private sealed class FakeEditorGateway : IReiEditorGateway
    {
        public int? RenamedEntityId { get; private set; }
        public string? RenamedEntityName { get; private set; }

        public Task<ReiEditorState> GetStateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReiEditorState(
                ReiEditorStatus.READY,
                new ReiProjectInfo("Symbols", "C:\\Repos\\Symbols", "C:\\Repos\\Symbols\\Symbols.rei_project", "C:\\Repos\\Symbols\\Symbols.sln"),
                new ReiSceneInfo("scene-1", "Main", 1),
                new ReiEngineInfo("running", "EditorMode")));
        }

        public Task<ReiEntityList> ListEntitiesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReiEntityList("scene-1", "Main", [CreateEntitySummary("Grid")]));
        }

        public Task<ReiEntityDetails> GetEntityAsync(int entityId, CancellationToken cancellationToken)
        {
            if (entityId != 42) throw new ReiMcpOperationException("entity_not_found", $"Entity {entityId} does not exist.");

            return Task.FromResult(new ReiEntityDetails(
                42,
                "Grid",
                0,
                0,
                [new ReiBehaviourDetails(1, "Transform", [])]));
        }

        public Task<ReiEntityMutationResult> RenameEntityAsync(int entityId, string newName, CancellationToken cancellationToken)
        {
            RenamedEntityId = entityId;
            RenamedEntityName = newName;
            return Task.FromResult(new ReiEntityMutationResult(true, CreateEntitySummary(newName), "Entity renamed."));
        }

        public Task<ReiProjectSaveResult> SaveProjectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReiProjectSaveResult(true, DateTimeOffset.UtcNow, "Project saved."));
        }

        private static ReiEntitySummary CreateEntitySummary(string name)
        {
            return new ReiEntitySummary(42, name, 0, 0, 0, [new ReiBehaviourSummary(1, "Transform")]);
        }
    }

    [Fact]
    public async Task HostExposesHealthAndRejectsUntrustedHostHeader()
    {
        await using var host = CreateHost(new FakeEditorGateway());
        await host.StartAsync();

        using var httpClient = new HttpClient();
        using var healthResponse = await httpClient.GetAsync(new Uri(host.Endpoint!, ReiMcpOptions.HEALTH_PATH));
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Contains("\"status\":\"ok\"", await healthResponse.Content.ReadAsStringAsync());

        using var invalidHostRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(host.Endpoint!, ReiMcpOptions.HEALTH_PATH));
        invalidHostRequest.Headers.Host = "attacker.example";
        using var invalidHostResponse = await httpClient.SendAsync(invalidHostRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalidHostResponse.StatusCode);
    }

    [Fact]
    public async Task McpClientListsAndCallsEditorToolsOverStreamableHttp()
    {
        var gateway = new FakeEditorGateway();
        await using var host = CreateHost(gateway);
        await host.StartAsync();

        await using var client = await CreateClient(host.Endpoint!);
        var tools = await client.ListToolsAsync();

        Assert.Equal(5, tools.Count);
        Assert.Contains(tools, x => x.Name == "rei_editor_get_state");
        Assert.Contains(tools, x => x.Name == "rei_editor_list_entities");
        Assert.Contains(tools, x => x.Name == "rei_editor_get_entity");
        Assert.Contains(tools, x => x.Name == "rei_editor_rename_entity");
        Assert.Contains(tools, x => x.Name == "rei_editor_save_project");

        var stateTool = Assert.Single(tools, x => x.Name == "rei_editor_get_state");
        Assert.True(stateTool.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.False(stateTool.ProtocolTool.Annotations?.OpenWorldHint);

        var saveTool = Assert.Single(tools, x => x.Name == "rei_editor_save_project");
        Assert.False(saveTool.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.True(saveTool.ProtocolTool.Annotations?.DestructiveHint);
        Assert.True(saveTool.ProtocolTool.Annotations?.IdempotentHint);
        Assert.False(saveTool.ProtocolTool.Annotations?.OpenWorldHint);

        var stateResult = await client.CallToolAsync("rei_editor_get_state", cancellationToken: CancellationToken.None);
        Assert.NotEqual(true, stateResult.IsError);
        Assert.Contains("\"status\":\"ready\"", GetText(stateResult));

        var renameResult = await client.CallToolAsync(
            "rei_editor_rename_entity",
            new Dictionary<string, object?>
            {
                ["entityId"] = 42,
                ["newName"] = "Symbols Grid"
            },
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, renameResult.IsError);
        Assert.Equal(42, gateway.RenamedEntityId);
        Assert.Equal("Symbols Grid", gateway.RenamedEntityName);
    }

    [Fact]
    public async Task McpToolReturnsSafeDomainErrorToClient()
    {
        await using var host = CreateHost(new FakeEditorGateway());
        await host.StartAsync();
        await using var client = await CreateClient(host.Endpoint!);

        var result = await client.CallToolAsync(
            "rei_editor_get_entity",
            new Dictionary<string, object?> { ["entityId"] = 999 },
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("entity_not_found", GetText(result));
        Assert.Contains("Entity 999 does not exist", GetText(result));
    }

    private static ReiMcpHost CreateHost(IReiEditorGateway gateway)
    {
        return new ReiMcpHost(new ReiMcpOptions { Port = 0 }, gateway);
    }

    private static async Task<McpClient> CreateClient(Uri endpoint)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp
        });

        return await McpClient.CreateAsync(transport, cancellationToken: CancellationToken.None);
    }

    private static string GetText(CallToolResult result)
    {
        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
    }
}
