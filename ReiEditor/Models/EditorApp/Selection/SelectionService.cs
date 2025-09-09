using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.EditorApp.Selection;

public class SelectionService : ISelectionService
{
    public IObservable<ISelectable?> ActiveSelection => _activeSelection;

    private readonly Observable<ISelectable?> _activeSelection = new(null);
    private readonly List<ISelectable> _selectables = new();
    
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

    public void Select(GameEntity e)
    {
        var s = _selectables.OfType<IEntitySelectable>().FirstOrDefault(x => x.Entity == e);
        if (s == null) return;
        
        Select(s);
    }

    public bool IsEntitySelected(GameEntity e)
    {
        if (_activeSelection.Value is IEntitySelectable es)
        {
            return es.Entity == e;
        }

        return false;
    }

    public void ResetSelection()
    {
        _activeSelection.Value = null;
        _entityApi.ResetEntitySelection();
    }

    public void RegisterSelectable(ISelectable selectable) => _selectables.Add(selectable);
    public void UnregisterSelectable(ISelectable selectable) => _selectables.RemoveAll(x => x == selectable);
}