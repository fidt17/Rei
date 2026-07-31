using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Mcp.Tools;

[McpServerToolType]
public sealed class ReiEditorMcpTools
{
    private readonly IReiEditorGateway _gateway;

    public ReiEditorMcpTools(IReiEditorGateway gateway)
    {
        _gateway = gateway;
    }

    [McpServerTool(Name = "rei_editor_get_state", Title = "Get Rei editor state", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns active project, scene, engine, import, build, and automation-operation state. Call before other editor tools.")]
    public Task<ReiEditorState> GetState(CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.GetStateAsync(cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_list_entities", Title = "List current scene entities", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists current scene hierarchy in display order, including entity ids, parent ids, depth, and attached behaviours.")]
    public Task<ReiEntityList> ListEntities(CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.ListEntitiesAsync(cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_get_entity", Title = "Inspect Rei entity", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns one current-scene entity with behaviour property types and JSON-compatible values.")]
    public Task<ReiEntityDetails> GetEntity(
        [Description("Stable entity id from rei_editor_list_entities.")] int entityId,
        CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.GetEntityAsync(entityId, cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_rename_entity", Title = "Rename Rei entity", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Renames one entity in current scene. Change remains in editor state until rei_editor_save_project is called.")]
    public Task<ReiEntityMutationResult> RenameEntity(
        [Description("Stable entity id from rei_editor_list_entities.")] int entityId,
        [Description("New non-empty entity name, maximum 128 characters.")] string newName,
        CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.RenameEntityAsync(entityId, newName, cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_save_project", Title = "Save Rei project", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Synchronizes current scene from engine and saves dirty project assets. Rejected during play mode, build, or another save.")]
    public Task<ReiProjectSaveResult> SaveProject(CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.SaveProjectAsync(cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_refresh_assets", Title = "Refresh Rei project assets", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts full project asset reimport, metadata update, behaviour registry refresh, and shader refresh. Returns operation id immediately.")]
    public Task<ReiOperationInfo> RefreshAssets(CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.StartAssetRefreshAsync(cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_start_build", Title = "Build Rei project", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts project solution and/or asset build through Editor pipeline. Returns operation id immediately; poll rei_editor_get_operation.")]
    public Task<ReiOperationInfo> StartBuild(
        [Description("Build configuration: debug, editor_debug, release, or editor_release.")] string configuration = ReiBuildConfigurations.EDITOR_DEBUG,
        [Description("Rebuild solution even when build cache is current.")] bool forceSolutionRebuild = false,
        [Description("Clean solution outputs before compilation.")] bool forceCleanSolutionBuild = false,
        [Description("Rebuild all assets even when build cache is current.")] bool forceAssetRebuild = false,
        [Description("Compile project solution.")] bool buildSolution = true,
        [Description("Build project assets.")] bool buildAssets = true,
        CancellationToken cancellationToken = default)
    {
        var options = new ReiBuildOptions(
            configuration,
            forceSolutionRebuild,
            forceCleanSolutionBuild,
            forceAssetRebuild,
            buildSolution,
            buildAssets);
        return Execute(() => _gateway.StartBuildAsync(options, cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_start_playmode", Title = "Start Rei play mode", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts save, incremental EditorDebug build, and play mode through Editor lifecycle. Returns operation id immediately.")]
    public Task<ReiOperationInfo> StartPlaymode(CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.StartPlaymodeAsync(cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_stop_playmode", Title = "Stop Rei play mode", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Stops active play mode. Editor mode restarts through existing Editor lifecycle. Returns operation id immediately.")]
    public Task<ReiOperationInfo> StopPlaymode(CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.StopPlaymodeAsync(cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_get_operation", Title = "Get Rei automation operation", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns current status, progress, timestamps, log count, and safe error for one refresh/build/play operation.")]
    public Task<ReiOperationInfo> GetOperation(
        [Description("Operation id returned by a start tool.")] string operationId,
        CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.GetOperationAsync(operationId, cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_cancel_operation", Title = "Cancel Rei automation operation", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Requests cancellation of active refresh/build/play operation. Some non-cancelable Editor phases finish before cancellation completes.")]
    public Task<ReiOperationInfo> CancelOperation(
        [Description("Operation id returned by a start tool.")] string operationId,
        CancellationToken cancellationToken)
    {
        return Execute(() => _gateway.CancelOperationAsync(operationId, cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_get_logs", Title = "Get Rei editor logs", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns Editor and engine logs, optionally from one automation operation. Operation logs survive console clearing.")]
    public Task<ReiLogList> GetLogs(
        [Description("Optional operation id. Omit for current Editor console snapshot.")] string? operationId = null,
        [Description("Minimum level: debug, info, warning, or error.")] string minimumLevel = "debug",
        [Description("Maximum newest entries to return, from 1 to 500.")] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return Execute(() => _gateway.GetLogsAsync(operationId, minimumLevel, limit, cancellationToken));
    }

    [McpServerTool(Name = "rei_editor_capture_frame", Title = "Capture Rei engine frame", ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Captures final engine framebuffer after post-processing, UI, and debug overlay. Returns PNG image content and frame metadata.")]
    public async Task<CallToolResult> CaptureFrame(CancellationToken cancellationToken)
    {
        var frame = await Execute(() => _gateway.CaptureFrameAsync(cancellationToken));
        var metadata = JsonSerializer.Serialize(new
        {
            width = frame.Width,
            height = frame.Height,
            capturedAtUtc = frame.CapturedAtUtc,
            engineMode = frame.EngineMode
        });

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock { Text = metadata },
                ImageContentBlock.FromBytes(frame.PngData, "image/png")
            ],
            IsError = false
        };
    }

    private static async Task<T> Execute<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (ReiMcpOperationException exception)
        {
            throw new McpException($"{exception.Code}: {exception.Message}");
        }
    }
}
