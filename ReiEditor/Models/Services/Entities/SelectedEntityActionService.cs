using ReiEditor.Models.EditorApp.Selection;

namespace ReiEditor.Models.Services.Entities;

public class SelectedEntityActionService : ISelectedEntityActionService
{
    public event System.Action<int>? RenameEntityRequested;

    private readonly ISelectionService _selectionService;
    private readonly IEntityManagementService _entityManagementService;

    public SelectedEntityActionService(
        ISelectionService selectionService,
        IEntityManagementService entityManagementService)
    {
        _selectionService = selectionService;
        _entityManagementService = entityManagementService;
    }

    public bool DeleteSelectedEntity()
    {
        if (_selectionService.ActiveSelection.Value is not IEntitySelectable entitySelection) return false;

        _selectionService.ResetSelection(sendToEngine: false);
        _entityManagementService.DestroyEntity(entitySelection.Entity);
        return true;
    }

    public bool DuplicateSelectedEntity()
    {
        if (_selectionService.ActiveSelection.Value is not IEntitySelectable entitySelection) return false;

        _entityManagementService.InstantiateEntity(entitySelection.Entity);
        return true;
    }

    public bool RequestRenameSelectedEntity()
    {
        if (_selectionService.ActiveSelection.Value is not IEntitySelectable entitySelection) return false;

        RenameEntityRequested?.Invoke(entitySelection.Entity.Id);
        return true;
    }
}
