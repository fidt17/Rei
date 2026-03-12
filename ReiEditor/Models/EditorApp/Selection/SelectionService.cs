using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.EditorApp.Selection;

public class SelectionService : ISelectionService
{
    public IObservable<ISelectable?> ActiveSelection => _activeSelection;
    public IReadOnlyCollection<ISelectable> SelectedItems => _selectedItems;

    private readonly Observable<ISelectable?> _activeSelection = new(null);
    private readonly HashSet<ISelectable> _selectedItems = new();
    private readonly List<ISelectable> _selectables = new();

    private readonly IEntityApi _entityApi;

    public SelectionService(IEntityApi entityApi)
    {
        _entityApi = entityApi;
    }

    public void Select(ISelectable selectable)
    {
        SetSelection(new[] { selectable }, selectable);
    }

    public void Select(GameEntity e, bool sendToEngine)
    {
        var selectable = _selectables.OfType<IEntitySelectable>().FirstOrDefault(x => x.Entity == e);
        if (selectable == null) return;

        if (sendToEngine)
        {
            _entityApi.ResetEntitySelection();
            _entityApi.SelectEntity(e.Id);
        }
        else if (_activeSelection.Value is IEntitySelectable && _activeSelection.Value != selectable)
        {
            _entityApi.ResetEntitySelection();
        }

        SetSelection(new[] { selectable }, selectable);
    }

    public void Deselect(ISelectable selectable, bool sendToEngine = true)
    {
        RemoveSelection(selectable, sendToEngine);
    }

    public void Deselect(GameEntity e, bool sendToEngine = true)
    {
        var selectable = _selectables.OfType<IEntitySelectable>().FirstOrDefault(x => x.Entity == e);
        if (selectable == null) return;

        RemoveSelection(selectable, sendToEngine);
    }

    public bool IsSelected(ISelectable selectable)
    {
        return _selectedItems.Contains(selectable);
    }

    public bool IsEntitySelected(GameEntity e)
    {
        return _selectedItems
            .OfType<IEntitySelectable>()
            .Any(selectable => selectable.Entity == e);
    }

    public void SetSelection(IReadOnlyCollection<ISelectable> selectables, ISelectable? primarySelection = null)
    {
        var selection = selectables
            .Where(selectable => selectable != null)
            .Distinct()
            .ToList();

        if (selection.Count == 0)
        {
            ResetSelection(sendToEngine: false);
            return;
        }

        var resolvedPrimary = primarySelection != null && selection.Contains(primarySelection)
            ? primarySelection
            : selection[0];

        if (_activeSelection.Value is IEntitySelectable && resolvedPrimary is not IEntitySelectable)
        {
            _entityApi.ResetEntitySelection();
        }

        _selectedItems.Clear();
        foreach (var selectable in selection)
        {
            _selectedItems.Add(selectable);
        }

        _activeSelection.Value = resolvedPrimary;
    }

    public void AddSelection(ISelectable selectable)
    {
        if (_activeSelection.Value is IEntitySelectable && selectable is not IEntitySelectable)
        {
            _entityApi.ResetEntitySelection();
        }

        _selectedItems.Add(selectable);
        _activeSelection.Value = selectable;
    }

    public void RemoveSelection(ISelectable selectable, bool sendToEngine = true)
    {
        if (!_selectedItems.Remove(selectable)) return;

        if (sendToEngine && selectable is IEntitySelectable)
        {
            _entityApi.ResetEntitySelection();
        }

        _activeSelection.Value = _selectedItems.FirstOrDefault();
    }

    public void ToggleSelection(ISelectable selectable)
    {
        if (_selectedItems.Contains(selectable))
        {
            RemoveSelection(selectable, sendToEngine: false);
            return;
        }

        AddSelection(selectable);
    }

    public void ResetSelection(bool sendToEngine)
    {
        if (sendToEngine && _activeSelection.Value is IEntitySelectable)
        {
            _entityApi.ResetEntitySelection();
        }

        _selectedItems.Clear();
        _activeSelection.Value = null;
    }

    public void RegisterSelectable(ISelectable selectable) => _selectables.Add(selectable);
    public void UnregisterSelectable(ISelectable selectable)
    {
        _selectables.RemoveAll(x => x == selectable);
        _selectedItems.Remove(selectable);

        if (_activeSelection.Value == selectable)
        {
            _activeSelection.Value = _selectedItems.FirstOrDefault();
        }
    }
}
