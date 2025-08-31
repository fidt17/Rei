using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.EditorApp.Selection;

public class SelectionService : ISelectionService
{
    public IObservable<ISelectable?> ActiveSelection => _activeSelection;

    private readonly Observable<ISelectable?> _activeSelection = new(null);
    private readonly IEntityApi _entityApi;

    public SelectionService(IEntityApi entityApi)
    {
        _entityApi = entityApi;
    }

    public void Select(ISelectable selectable)
    {
        _activeSelection.Value = selectable;

        if (selectable is IEntitySelectable entitySelectable)
        {
            _entityApi.ResetEntitySelection();
            _entityApi.SelectEntity(entitySelectable.Entity.Id);
        }
    }

    public void ResetSelection()
    {
        _activeSelection.Value = null;
        _entityApi.ResetEntitySelection();
    }
}