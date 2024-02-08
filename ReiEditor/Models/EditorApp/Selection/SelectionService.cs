using ReiEditor.Utils.Common;

namespace ReiEditor.Models.EditorApp.Selection;

public class SelectionService : ISelectionService
{
    public IObservable<ISelectable?> ActiveSelection => _activeSelection;

    private readonly Observable<ISelectable?> _activeSelection = new(null);

    public void Select(ISelectable selectable)
    {
        _activeSelection.Value = selectable;
    }

    public void ResetSelection()
    {
        _activeSelection.Value = null;
    }
}