using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.EditorApp.Selection;

namespace ReiEditor.Models.Services.Entities.Sync;

public class EngineSelectionSyncService
{
    private readonly ISelectionService _selectionService;

    public EngineSelectionSyncService(ISelectionService selectionService)
    {
        _selectionService = selectionService;
    }

    public void ApplySelection(IReadOnlyCollection<GameEntity> entities, IReadOnlySet<int> selectedEntityIds)
    {
        using var timing = ReiEditor.Utils.Diagnostics.EditorInteractionTiming.Measure("Selection.ApplyNative", count: selectedEntityIds.Count);
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

}
