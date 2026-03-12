using System.Collections.Generic;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.EditorApp.Selection;

public interface ISelectionService
{
    Utils.Common.IObservable<ISelectable?> ActiveSelection { get; }
    Utils.Common.IObservable<IReadOnlyCollection<ISelectable>> SelectionChanged { get; }
    IReadOnlyCollection<ISelectable> SelectedItems { get; }

    void Select(ISelectable selectable);
    void Select(GameEntity e, bool sendToEngine = true);
    void AddSelection(GameEntity e, bool sendToEngine = true);
    
    void Deselect(ISelectable selectable, bool sendToEngine = true);
    void Deselect(GameEntity e, bool sendToEngine = true);

    bool IsSelected(ISelectable selectable);
    bool IsEntitySelected(GameEntity e);
    IEntitySelectable? GetEntitySelectable(GameEntity e);
    
    void SetSelection(IReadOnlyCollection<ISelectable> selectables, ISelectable? primarySelection = null, bool sendToEngine = true);
    void AddSelection(ISelectable selectable, bool sendToEngine = true);
    void RemoveSelection(ISelectable selectable, bool sendToEngine = true);
    void ToggleSelection(ISelectable selectable, bool sendToEngine = true);
    
    void ResetSelection(bool sendToEngine = true);

    void RegisterSelectable(ISelectable selectable);
    void UnregisterSelectable(ISelectable selectable);
}
