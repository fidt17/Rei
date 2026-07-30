using System.ComponentModel;
using ModelContextProtocol;
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
    [Description("Returns active project, scene, engine mode, and editor readiness. Call before project or scene operations.")]
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
