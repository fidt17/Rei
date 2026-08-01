using System.Net;
using System.Text.Json;
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
        private static readonly DateTimeOffset NOW = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        private static readonly byte[] PNG_DATA = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

        public int? RenamedEntityId { get; private set; }
        public string? RenamedEntityName { get; private set; }
        public ReiBuildOptions? BuildOptions { get; private set; }
        public int? AddedBehaviourEntityId { get; private set; }
        public string? AddedBehaviourName { get; private set; }
        public string? SetPropertyBehaviourName { get; private set; }
        public string? SetPropertyName { get; private set; }
        public object? SetPropertyValue { get; private set; }
        public string? MaterialAssetId { get; private set; }
        public string? MaterialPropertyName { get; private set; }
        public object? MaterialPropertyValue { get; private set; }

        public Task<ReiEditorState> GetStateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReiEditorState(
                ReiEditorStatus.READY,
                new ReiProjectInfo("Symbols", @"C:\Repos\Symbols", @"C:\Repos\Symbols\Symbols.rei_project", @"C:\Repos\Symbols\Symbols.sln"),
                new ReiSceneInfo("scene-1", "Main", 1),
                new ReiEngineInfo("running", "EditorMode"),
                new ReiAutomationState(false, false, null)));
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

        public Task<ReiBehaviourMutationResult> AddBehaviourAsync(
            int entityId,
            string behaviourName,
            CancellationToken cancellationToken)
        {
            AddedBehaviourEntityId = entityId;
            AddedBehaviourName = behaviourName;
            return Task.FromResult(new ReiBehaviourMutationResult(
                true,
                CreateEntityDetails(behaviourName),
                "Behaviour added."));
        }

        public Task<ReiBehaviourPropertyMutationResult> SetBehaviourPropertyAsync(
            int entityId,
            string behaviourName,
            string propertyName,
            object? value,
            CancellationToken cancellationToken)
        {
            SetPropertyBehaviourName = behaviourName;
            SetPropertyName = propertyName;
            SetPropertyValue = value;
            var property = new ReiPropertyDetails(propertyName, "Integer", "i32", 64);
            return Task.FromResult(new ReiBehaviourPropertyMutationResult(
                true,
                entityId,
                new ReiBehaviourDetails(5, behaviourName, [property]),
                property,
                "Behaviour property changed."));
        }

        public Task<ReiMaterialPropertyMutationResult> SetMaterialPropertyAsync(
            string materialAssetId,
            string propertyName,
            object? value,
            CancellationToken cancellationToken)
        {
            MaterialAssetId = materialAssetId;
            MaterialPropertyName = propertyName;
            MaterialPropertyValue = value;
            var property = new ReiPropertyDetails(
                propertyName,
                "Texture",
                "sampler2D",
                new Dictionary<string, object?> { ["Id"] = "texture-1" });
            return Task.FromResult(new ReiMaterialPropertyMutationResult(
                true,
                materialAssetId,
                "shader-1",
                property,
                true,
                "Material property changed."));
        }

        public Task<ReiProjectSaveResult> SaveProjectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReiProjectSaveResult(true, NOW, "Project saved."));
        }

        public Task<ReiOperationInfo> StartAssetRefreshAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateOperation("refresh-1", ReiOperationKinds.REFRESH_ASSETS));
        }

        public Task<ReiOperationInfo> StartBuildAsync(ReiBuildOptions options, CancellationToken cancellationToken)
        {
            BuildOptions = options;
            return Task.FromResult(CreateOperation("build-1", ReiOperationKinds.BUILD_PROJECT));
        }

        public Task<ReiOperationInfo> StartPlaymodeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateOperation("play-1", ReiOperationKinds.START_PLAYMODE));
        }

        public Task<ReiOperationInfo> StopPlaymodeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateOperation("stop-1", ReiOperationKinds.STOP_PLAYMODE));
        }

        public Task<ReiOperationInfo> GetOperationAsync(string operationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateOperation(operationId, ReiOperationKinds.BUILD_PROJECT));
        }

        public Task<ReiOperationInfo> CancelOperationAsync(string operationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateOperation(operationId, ReiOperationKinds.BUILD_PROJECT) with
            {
                Status = ReiOperationStatuses.CANCELED,
                Message = "Operation canceled.",
                CompletedAtUtc = NOW
            });
        }

        public Task<ReiLogList> GetLogsAsync(string? operationId, string minimumLevel, int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReiLogList(
                operationId,
                1,
                false,
                [new ReiLogEntry(NOW, "editor", "info", "Build complete.", string.Empty)]));
        }

        public Task<ReiFrameCapture> CaptureFrameAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReiFrameCapture(PNG_DATA, 1, 1, NOW, "EditorMode"));
        }

        private static ReiOperationInfo CreateOperation(string id, string kind)
        {
            return new ReiOperationInfo(
                id,
                kind,
                ReiOperationStatuses.RUNNING,
                0.25,
                "Running.",
                NOW,
                NOW,
                null,
                1,
                null);
        }

        private static ReiEntitySummary CreateEntitySummary(string name)
        {
            return new ReiEntitySummary(42, name, 0, 0, 0, [new ReiBehaviourSummary(1, "Transform")]);
        }

        private static ReiEntityDetails CreateEntityDetails(string behaviourName)
        {
            return new ReiEntityDetails(
                42,
                "Grid",
                0,
                0,
                [new ReiBehaviourDetails(5, behaviourName, [])]);
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

        Assert.Equal(16, tools.Count);
        Assert.Contains(tools, x => x.Name == "rei_editor_get_state");
        Assert.Contains(tools, x => x.Name == "rei_editor_list_entities");
        Assert.Contains(tools, x => x.Name == "rei_editor_get_entity");
        Assert.Contains(tools, x => x.Name == "rei_editor_rename_entity");
        Assert.Contains(tools, x => x.Name == "rei_editor_add_behaviour");
        Assert.Contains(tools, x => x.Name == "rei_editor_set_behaviour_property");
        Assert.Contains(tools, x => x.Name == "rei_editor_set_material_property");
        Assert.Contains(tools, x => x.Name == "rei_editor_save_project");
        Assert.Contains(tools, x => x.Name == "rei_editor_refresh_assets");
        Assert.Contains(tools, x => x.Name == "rei_editor_start_build");
        Assert.Contains(tools, x => x.Name == "rei_editor_start_playmode");
        Assert.Contains(tools, x => x.Name == "rei_editor_stop_playmode");
        Assert.Contains(tools, x => x.Name == "rei_editor_get_operation");
        Assert.Contains(tools, x => x.Name == "rei_editor_cancel_operation");
        Assert.Contains(tools, x => x.Name == "rei_editor_get_logs");
        Assert.Contains(tools, x => x.Name == "rei_editor_capture_frame");

        var stateTool = Assert.Single(tools, x => x.Name == "rei_editor_get_state");
        Assert.True(stateTool.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.False(stateTool.ProtocolTool.Annotations?.OpenWorldHint);

        var saveTool = Assert.Single(tools, x => x.Name == "rei_editor_save_project");
        Assert.False(saveTool.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.True(saveTool.ProtocolTool.Annotations?.DestructiveHint);
        Assert.True(saveTool.ProtocolTool.Annotations?.IdempotentHint);
        Assert.False(saveTool.ProtocolTool.Annotations?.OpenWorldHint);

        var captureTool = Assert.Single(tools, x => x.Name == "rei_editor_capture_frame");
        Assert.True(captureTool.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.False(captureTool.ProtocolTool.Annotations?.IdempotentHint);

        var stateResult = await client.CallToolAsync("rei_editor_get_state", cancellationToken: CancellationToken.None);
        Assert.NotEqual(true, stateResult.IsError);
        Assert.Contains("\"status\":\"ready\"", GetText(stateResult));
        Assert.Contains("\"isImporting\":false", GetText(stateResult));
        Assert.Contains("\"activeOperation\":null", GetText(stateResult));

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

        var addBehaviourResult = await client.CallToolAsync(
            "rei_editor_add_behaviour",
            new Dictionary<string, object?>
            {
                ["entityId"] = 42,
                ["behaviourName"] = "SymbolGridBehaviour"
            },
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, addBehaviourResult.IsError);
        Assert.Equal(42, gateway.AddedBehaviourEntityId);
        Assert.Equal("SymbolGridBehaviour", gateway.AddedBehaviourName);

        var setPropertyResult = await client.CallToolAsync(
            "rei_editor_set_behaviour_property",
            new Dictionary<string, object?>
            {
                ["entityId"] = 42,
                ["behaviourName"] = "SymbolGridBehaviour",
                ["propertyName"] = "_columns",
                ["value"] = 64
            },
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, setPropertyResult.IsError);
        Assert.Equal("SymbolGridBehaviour", gateway.SetPropertyBehaviourName);
        Assert.Equal("_columns", gateway.SetPropertyName);
        var propertyValue = Assert.IsType<JsonElement>(gateway.SetPropertyValue);
        Assert.Equal(64, propertyValue.GetInt32());
        Assert.Contains("\"value\":64", GetText(setPropertyResult));

        var setMaterialResult = await client.CallToolAsync(
            "rei_editor_set_material_property",
            new Dictionary<string, object?>
            {
                ["materialAssetId"] = "material-1",
                ["propertyName"] = "_MainTex",
                ["value"] = new Dictionary<string, object?> { ["Id"] = "texture-1" }
            },
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, setMaterialResult.IsError);
        Assert.Equal("material-1", gateway.MaterialAssetId);
        Assert.Equal("_MainTex", gateway.MaterialPropertyName);
        var materialValue = Assert.IsType<JsonElement>(gateway.MaterialPropertyValue);
        Assert.Equal("texture-1", materialValue.GetProperty("Id").GetString());
        Assert.Contains("\"runtimeSynced\":true", GetText(setMaterialResult));

        var buildResult = await client.CallToolAsync(
            "rei_editor_start_build",
            new Dictionary<string, object?>
            {
                ["configuration"] = ReiBuildConfigurations.EDITOR_RELEASE,
                ["forceSolutionRebuild"] = true,
                ["buildAssets"] = false
            },
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, buildResult.IsError);
        Assert.Contains("\"id\":\"build-1\"", GetText(buildResult));
        Assert.Contains("\"completedAtUtc\":null", GetText(buildResult));
        Assert.Contains("\"error\":null", GetText(buildResult));
        Assert.Equal(ReiBuildConfigurations.EDITOR_RELEASE, gateway.BuildOptions?.Configuration);
        Assert.True(gateway.BuildOptions?.ForceSolutionRebuild);
        Assert.False(gateway.BuildOptions?.BuildAssets);

        var logsResult = await client.CallToolAsync(
            "rei_editor_get_logs",
            new Dictionary<string, object?> { ["operationId"] = "build-1" },
            cancellationToken: CancellationToken.None);
        Assert.NotEqual(true, logsResult.IsError);
        Assert.Contains("Build complete", GetText(logsResult));
    }

    [Fact]
    public async Task CaptureFrameReturnsMetadataAndPngImageContent()
    {
        await using var host = CreateHost(new FakeEditorGateway());
        await host.StartAsync();
        await using var client = await CreateClient(host.Endpoint!);

        var result = await client.CallToolAsync("rei_editor_capture_frame", cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("\"width\":1", GetText(result));
        var image = Assert.Single(result.Content.OfType<ImageContentBlock>());
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(new byte[] {137, 80, 78, 71, 13, 10, 26, 10}, image.DecodedData.Span[..8].ToArray());
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
