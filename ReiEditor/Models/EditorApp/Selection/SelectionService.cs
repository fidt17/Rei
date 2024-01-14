using System;

namespace ReiEditor.Models.EditorApp.Selection;

public class SelectionService : ISelectionService
{
    public event Action<ISelectable?>? SelectionChangedEvent;

    private ISelectable? _selectable;
    
    public void Select(ISelectable selectable)
    {
        _selectable = selectable;
        InvokeSelectionChangedEvent();
    }

    public void ResetSelection()
    {
        _selectable = null;
        InvokeSelectionChangedEvent();
    }

    private void InvokeSelectionChangedEvent() => SelectionChangedEvent?.Invoke(_selectable);
}