using System.Collections.Generic;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.EditorApp.Selection;

public interface ISelectionService
{
    Utils.Common.IObservable<ISelectable?> ActiveSelection { get; }
    IReadOnlyCollection<ISelectable> SelectedItems { get; }

    void Select(ISelectable selectable);
    void Select(GameEntity e, bool sendToEngine = true);
    
    void Deselect(ISelectable selectable, bool sendToEngine = true);
    void Deselect(GameEntity e, bool sendToEngine = true);

    bool IsSelected(ISelectable selectable);
    bool IsEntitySelected(GameEntity e);
    
    void SetSelection(IReadOnlyCollection<ISelectable> selectables, ISelectable? primarySelection = null);
    void AddSelection(ISelectable selectable);
    void RemoveSelection(ISelectable selectable, bool sendToEngine = true);
    void ToggleSelection(ISelectable selectable);
    
    void ResetSelection(bool sendToEngine = true);

    void RegisterSelectable(ISelectable selectable);
    void UnregisterSelectable(ISelectable selectable);
}
