using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.EditorApp.Selection;

public interface ISelectionService
{
    Utils.Common.IObservable<ISelectable?> ActiveSelection { get; }

    void Select(ISelectable selectable);
    void Select(GameEntity e);

    bool IsEntitySelected(GameEntity e);
    
    void ResetSelection();

    void RegisterSelectable(ISelectable selectable);
    void UnregisterSelectable(ISelectable selectable);
}