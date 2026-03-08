using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Entities;

public class SelectedEntityEditorActionService : ISelectedEntityEditorActionService
{
    public event System.Action<int>? RenameEntityRequested;

    private readonly ISelectionService _selectionService;
    private readonly IEntityManagementService _entityManagementService;
    private readonly IEngineRunner _engineRunner;

    private const int GLFW_KEY_DELETE = 261;
    private const int GLFW_KEY_F2 = 291;
    private const int GLFW_KEY_D = 68;
    private const int GLFW_MOD_CONTROL = 0x0002;

    public SelectedEntityEditorActionService(
        ISelectionService selectionService,
        IEntityManagementService entityManagementService,
        IEngineEditorInputService engineEditorInputService,
        IEngineRunner engineRunner)
    {
        _selectionService = selectionService;
        _entityManagementService = entityManagementService;
        _engineRunner = engineRunner;

        engineEditorInputService.InputReceivedEvent += HandleEngineEditorInputEvent;
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

    private void HandleEngineEditorInputEvent(EngineEditorInputEvent inputEvent)
    {
        if (!_engineRunner.IsEditorActive.Value) return;
        if (inputEvent.Type != EngineEditorInputEventType.KeyDown) return;

        if (inputEvent.Code == GLFW_KEY_DELETE)
        {
            DeleteSelectedEntity();
            return;
        }

        if (inputEvent.Code == GLFW_KEY_F2)
        {
            RequestRenameSelectedEntity();
            return;
        }

        if (inputEvent.Code == GLFW_KEY_D && (inputEvent.Mods & GLFW_MOD_CONTROL) != 0)
        {
            DuplicateSelectedEntity();
        }
    }
}
