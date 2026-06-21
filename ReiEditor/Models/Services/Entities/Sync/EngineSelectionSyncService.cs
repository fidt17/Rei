using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Entities.Sync;

public class EngineSelectionSyncService
{
    private readonly ISelectionService _selectionService;

    public EngineSelectionSyncService(ISelectionService selectionService)
    {
        _selectionService = selectionService;
    }

    public bool TryApplySelectionSnapshot(
        IReadOnlyCollection<GameEntity> currentSceneEntities,
        GetSceneEntitiesResponse entities,
        IReadOnlySet<int> selectionBeforeSync)
    {
        var selectedEntityIds = entities.Entities
            .Where(entity => entity.IsSelected)
            .Select(entity => entity.Id)
            .ToHashSet();
        var currentSceneEntityIds = currentSceneEntities
            .Select(entity => entity.Id)
            .ToHashSet();

        if (selectedEntityIds.Any(entityId => !currentSceneEntityIds.Contains(entityId))) return false;

        Dispatcher.UIThread.Invoke(() =>
        {
            if (!selectionBeforeSync.SetEquals(CaptureEditorSelectedEntityIds())) return;

            ApplySelection(currentSceneEntities, selectedEntityIds);
        });

        return true;
    }

    public void ApplySelection(IReadOnlyCollection<GameEntity> entities, IReadOnlySet<int> selectedEntityIds)
    {
        var selectedItems = entities
            .Where(entity => selectedEntityIds.Contains(entity.Id))
            .Select(entity => _selectionService.GetEntitySelectable(entity))
            .Where(selectable => selectable != null)
            .Cast<ISelectable>()
            .ToList();

        if (selectedItems.Count == 0)
        {
            if (_selectionService.SelectedItems.OfType<IEntitySelectable>().Any())
            {
                _selectionService.ResetSelection(sendToEngine: false);
            }

            return;
        }

        var currentPrimarySelection = _selectionService.ActiveSelection.Value as IEntitySelectable;
        var primarySelection = currentPrimarySelection != null &&
                               selectedEntityIds.Contains(currentPrimarySelection.Entity.Id)
            ? currentPrimarySelection
            : selectedItems.OfType<IEntitySelectable>().FirstOrDefault();
        if (primarySelection == null)
        {
            _selectionService.ResetSelection(sendToEngine: false);
            return;
        }

        _selectionService.SetSelection(selectedItems, primarySelection, sendToEngine: false);
    }

    public HashSet<int> CaptureEditorSelectedEntityIds()
    {
        return _selectionService.SelectedItems
            .OfType<IEntitySelectable>()
            .Select(selectable => selectable.Entity.Id)
            .ToHashSet();
    }
}
