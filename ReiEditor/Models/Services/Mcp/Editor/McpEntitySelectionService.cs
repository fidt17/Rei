using System;
using System.Collections.Generic;
using System.Linq;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal sealed class McpEntitySelectionService(ISelectionService selection, ISceneManagementService scenes, IEntityApi entities, IEngineNativeAccess native) : IMcpEntitySelectionService
{
    public ReiSelectionState Read(string source)
    {
        if (source == "editor") return new(source, "ok", selection.SelectedItems.OfType<IEntitySelectable>().Select(item => item.Entity.Id).Distinct().Order().ToArray());
        if (source != "runtime") throw new ReiMcpOperationException("invalid_source", "Selection source must be editor or runtime.");

        ReiSelectionState result = new(source, "read_failed", null);
        try
        {
            if (!native.TryInvoke(_ =>
            {
                // Fresh native EntityInfo/SelectedTag read; never use callback or Editor caches.
                var state = entities.GetSceneEntities();
                if (state != null) result = new(source, "ok", state.Entities.Where(entity => entity.IsSelected).Select(entity => entity.Id).Distinct().Order().ToArray());
            })) return new(source, "engine_unavailable", null);
        }
        catch (Exception) { return new(source, "read_failed", null); }
        return result;
    }

    public ReiSelectionState Set(IReadOnlyList<int> entityIds)
    {
        if (entityIds == null) throw new ReiMcpOperationException("invalid_entity_ids", "Entity ids must be an array.");
        var scene = scenes.CurrentScene.Value ?? throw new ReiMcpOperationException("scene_unavailable", "Current scene is not ready.");
        var selectables = new List<ISelectable>();
        // Validate entire set before changing selection; duplicate ids preserve first primary.
        foreach (var id in entityIds.Distinct())
        {
            var entity = scene.GetById(id) ?? throw new ReiMcpOperationException("entity_not_found", $"Entity {id} does not exist.");
            var selectable = selection.GetEntitySelectable(entity) ?? throw new ReiMcpOperationException("entity_not_selectable", $"Entity {id} has no registered hierarchy selection.");
            selectables.Add(selectable);
        }
        selection.SetSelection(selectables, selectables.FirstOrDefault());
        return Read("editor");
    }
}
