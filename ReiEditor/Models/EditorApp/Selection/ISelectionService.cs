using System;

namespace ReiEditor.Models.EditorApp.Selection;

public interface ISelectionService
{
    event Action<ISelectable?> SelectionChangedEvent;
    
    void Select(ISelectable selectable);
    void ResetSelection();
}