using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.EditorApp.Selection;

public class SelectionService : ISelectionService
{
    public IObservable<ISelectable?> ActiveSelection => _activeSelection;
    public IObservable<IReadOnlyCollection<ISelectable>> SelectionChanged => _selectionChanged;
    public IReadOnlyCollection<ISelectable> SelectedItems => _selectedItems;

    private readonly Observable<ISelectable?> _activeSelection = new(null);
    private readonly Observable<IReadOnlyCollection<ISelectable>> _selectionChanged = new(System.Array.Empty<ISelectable>());
    private readonly HashSet<ISelectable> _selectedItems = new();
    private readonly List<ISelectable> _selectables = new();
    private HashSet<int> _syncedEntityIds = new();

    private readonly IEntityApi _entityApi;

    public SelectionService(IEntityApi entityApi)
    {
        _entityApi = entityApi;
    }

    public void Select(ISelectable selectable)
    {
        SetSelection(new[] { selectable }, selectable);
    }

    public void Select(GameEntity e, bool sendToEngine = true)
    {
        var selectable = GetEntitySelectable(e);
        if (selectable == null) return;

        SetSelection(new[] { selectable }, selectable, sendToEngine);
    }

    public void AddSelection(GameEntity e, bool sendToEngine = true)
    {
        var selectable = GetEntitySelectable(e);
        if (selectable == null) return;

        AddSelection(selectable, sendToEngine);
    }

    public void Deselect(ISelectable selectable, bool sendToEngine = true)
    {
        RemoveSelection(selectable, sendToEngine);
    }

    public void Deselect(GameEntity e, bool sendToEngine = true)
    {
        var selectable = GetEntitySelectable(e);
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

    public IEntitySelectable? GetEntitySelectable(GameEntity e)
    {
        return _selectables.OfType<IEntitySelectable>().FirstOrDefault(x => x.Entity == e);
    }

    public void SetSelection(IReadOnlyCollection<ISelectable> selectables, ISelectable? primarySelection = null, bool sendToEngine = true)
    {
        var selection = selectables
            .Distinct()
            .ToList();
        if (selection.Count == 0)
        {
            ResetSelection(sendToEngine);
            return;
        }

        var resolvedPrimary = primarySelection != null && selection.Contains(primarySelection)
            ? primarySelection
            : selection[0];

        if (SelectionEquals(selection, resolvedPrimary))
        {
            SyncEntitySelection(selection, sendToEngine);
            return;
        }

        _selectedItems.Clear();
        foreach (var selectable in selection)
        {
            _selectedItems.Add(selectable);
        }

        SyncEntitySelection(selection, sendToEngine);
        PublishSelectionChanged(resolvedPrimary);
    }

    public void AddSelection(ISelectable selectable, bool sendToEngine = true)
    {
        var selection = _selectedItems.ToList();
        if (!selection.Contains(selectable))
        {
            selection.Add(selectable);
        }

        SetSelection(selection, selectable, sendToEngine);
    }

    public void RemoveSelection(ISelectable selectable, bool sendToEngine = true)
    {
        if (!_selectedItems.Contains(selectable)) return;

        var selection = _selectedItems
            .Where(item => item != selectable)
            .ToList();
        var primarySelection = _activeSelection.Value == selectable
            ? selection.FirstOrDefault()
            : _activeSelection.Value;

        if (selection.Count == 0)
        {
            ResetSelection(sendToEngine);
            return;
        }

        SetSelection(selection, primarySelection, sendToEngine);
    }

    public void ToggleSelection(ISelectable selectable, bool sendToEngine = true)
    {
        if (_selectedItems.Contains(selectable))
        {
            RemoveSelection(selectable, sendToEngine);
            return;
        }

        AddSelection(selectable, sendToEngine);
    }

    public void ResetSelection(bool sendToEngine = true)
    {
        if (_selectedItems.Count == 0 && _activeSelection.Value == null)
        {
            SyncEntitySelection(System.Array.Empty<ISelectable>(), sendToEngine);
            return;
        }

        _selectedItems.Clear();
        SyncEntitySelection(System.Array.Empty<ISelectable>(), sendToEngine);
        PublishSelectionChanged(null);
    }

    public void RegisterSelectable(ISelectable selectable) => _selectables.Add(selectable);

    public void UnregisterSelectable(ISelectable selectable)
    {
        _selectables.RemoveAll(x => x == selectable);
    }

    private bool SelectionEquals(IReadOnlyCollection<ISelectable> selection, ISelectable primarySelection)
    {
        return _selectedItems.Count == selection.Count &&
               _selectedItems.SetEquals(selection) &&
               ReferenceEquals(_activeSelection.Value, primarySelection);
    }

    private void SyncEntitySelection(IReadOnlyCollection<ISelectable> selection, bool sendToEngine)
    {
        var entityIds = selection
            .OfType<IEntitySelectable>()
            .Select(selectable => selectable.Entity.Id)
            .Distinct()
            .ToHashSet();

        var hasNonEntitySelection = selection.Any(selectable => selectable is not IEntitySelectable);
        if (hasNonEntitySelection)
        {
            entityIds.Clear();
        }

        if (sendToEngine)
        {
            if (!_syncedEntityIds.SetEquals(entityIds))
            {
                _entityApi.SetEntitySelection(new SetEntitySelectionRequest
                {
                    EntityIds = entityIds.ToList()
                });
            }
        }

        _syncedEntityIds = entityIds;
    }

    private void PublishSelectionChanged(ISelectable? primarySelection)
    {
        _activeSelection.Value = primarySelection;
        _selectionChanged.SetAndInvoke(_selectedItems.ToArray());
    }
}
