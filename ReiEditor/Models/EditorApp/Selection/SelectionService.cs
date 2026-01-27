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
        if (_activeSelection.Value == selectable) return;
        
        _activeSelection.Value = selectable;
        selectable.Select();
    }

    public void Select(GameEntity e, bool sendToEngine)
    {
        var s = _selectables.OfType<IEntitySelectable>().FirstOrDefault(x => x.Entity == e);
        if (s == null) return;

        if (_activeSelection.Value == s) return;
        
        Select(s);

        if (sendToEngine)
        {
            _entityApi.ResetEntitySelection();
            _entityApi.SelectEntity(e.Id);
        }
    }

    public bool IsEntitySelected(GameEntity e)
    {
        if (_activeSelection.Value is IEntitySelectable es)
        {
            return es.Entity == e;
        }

        return false;
    }

    public void ResetSelection(bool sendToEngine)
    {
        _activeSelection.Value = null;
        if (sendToEngine)
        {
            _entityApi.ResetEntitySelection();
        }
    }

    public void RegisterSelectable(ISelectable selectable) => _selectables.Add(selectable);
    public void UnregisterSelectable(ISelectable selectable) => _selectables.RemoveAll(x => x == selectable);
}