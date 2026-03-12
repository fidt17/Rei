using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Scenes;
using System.Collections.Generic;
using System.Linq;

namespace ReiEditor.Models.Services.Entities;

public class SelectedEntityActionService : ISelectedEntityActionService
{
    public event System.Action<int>? RenameEntityRequested;

    private bool _isExecutingAction;

    private readonly ISelectionService _selectionService;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IEntityApi _entityApi;
    private readonly ISelectedEntityDeleteCommand _deleteCommand;
    private readonly ISelectedEntityDuplicateCommand _duplicateCommand;
    private readonly ISelectedEntityRenameCommand _renameCommand;

    public SelectedEntityActionService(
        ISelectionService selectionService,
        ISceneManagementService sceneManagementService,
        IEntityApi entityApi,
        ISelectedEntityDeleteCommand deleteCommand,
        ISelectedEntityDuplicateCommand duplicateCommand,
        ISelectedEntityRenameCommand renameCommand)
    {
        _selectionService = selectionService;
        _sceneManagementService = sceneManagementService;
        _entityApi = entityApi;
        _deleteCommand = deleteCommand;
        _duplicateCommand = duplicateCommand;
        _renameCommand = renameCommand;
    }

    public bool DeleteSelectedEntity()
    {
        if (_isExecutingAction) return false;

        var target = ResolveTarget();
        if (target == null) return false;

        _isExecutingAction = true;
        try
        {
            _selectionService.ResetSelection();
            _deleteCommand.Execute(target);
            return true;
        }
        finally
        {
            _isExecutingAction = false;
        }
    }

    public bool DuplicateSelectedEntity()
    {
        if (_isExecutingAction) return false;

        var target = ResolveTarget();
        if (target == null) return false;

        _isExecutingAction = true;
        try
        {
            var result = _duplicateCommand.Execute(target);
            ApplyDuplicateSelection(result);
            return true;
        }
        finally
        {
            _isExecutingAction = false;
        }
    }

    public bool RequestRenameSelectedEntity()
    {
        if (_isExecutingAction) return false;

        var target = ResolveTarget();
        if (target == null) return false;

        _isExecutingAction = true;
        try
        {
            var result = _renameCommand.Execute(target);
            if (result.RenameEntityId.HasValue)
            {
                RenameEntityRequested?.Invoke(result.RenameEntityId.Value);
            }

            return true;
        }
        finally
        {
            _isExecutingAction = false;
        }
    }

    private SelectedEntityCommandTarget? ResolveTarget()
    {
        var scene = _sceneManagementService.CurrentScene.Value;

        if (_selectionService.ActiveSelection.Value is not IEntitySelectable primarySelection)
        {
            return null;
        }

        if (scene != null && !scene.Entities.Any(entity => entity.Id == primarySelection.Entity.Id))
        {
            return null;
        }

        var selectedEntities = _selectionService.SelectedItems
            .OfType<IEntitySelectable>()
            .Select(selectable => selectable.Entity)
            .Where(entity => scene == null || scene.Entities.Any(sceneEntity => sceneEntity.Id == entity.Id))
            .DistinctBy(entity => entity.Id)
            .ToList();
        if (selectedEntities.Count == 0)
        {
            selectedEntities.Add(primarySelection.Entity);
        }

        var topLevelEntities = CollapseNestedSelection(selectedEntities);
        return new SelectedEntityCommandTarget(primarySelection.Entity, topLevelEntities);
    }

    private List<GameEntity> CollapseNestedSelection(IReadOnlyList<GameEntity> selectedEntities)
    {
        var scene = _sceneManagementService.CurrentScene.Value;
        if (scene == null)
        {
            return selectedEntities
                .DistinctBy(entity => entity.Id)
                .ToList();
        }

        var distinctEntities = selectedEntities
            .DistinctBy(entity => entity.Id)
            .ToList();

        var entityById = scene.Entities.ToDictionary(entity => entity.Id);
        var selectedEntityIds = distinctEntities
            .Select(entity => entity.Id)
            .ToHashSet();

        return distinctEntities
            .Where(entity => entityById.ContainsKey(entity.Id))
            .Where(entity => !HasSelectedAncestor(entity, selectedEntityIds, entityById))
            .OrderBy(entity => entity.Transform.Order)
            .ThenBy(entity => entity.Id)
            .ToList();
    }

    private static bool HasSelectedAncestor(
        GameEntity entity,
        IReadOnlySet<int> selectedEntityIds,
        IReadOnlyDictionary<int, GameEntity> entityById)
    {
        var parentId = entity.Transform.Parent;
        while (parentId != 0)
        {
            if (selectedEntityIds.Contains(parentId))
            {
                return true;
            }

            if (!entityById.TryGetValue(parentId, out var parentEntity))
            {
                return false;
            }

            parentId = parentEntity.Transform.Parent;
        }

        return false;
    }

    private void ApplyDuplicateSelection(SelectedEntityCommandResult result)
    {
        if (result.SelectedEntityIds == null || result.SelectedEntityIds.Count == 0) return;

        _entityApi.SetEntitySelection(new SetEntitySelectionRequest
        {
            EntityIds = result.SelectedEntityIds.ToList()
        });

        var scene = _sceneManagementService.CurrentScene.Value;
        if (scene == null) return;

        var selectedItems = scene.Entities
            .Where(entity => result.SelectedEntityIds.Contains(entity.Id))
            .Select(entity => _selectionService.GetEntitySelectable(entity))
            .Where(selectable => selectable != null)
            .Cast<ISelectable>()
            .ToList();
        if (selectedItems.Count == 0) return;

        var primaryEntityId = result.SelectedEntityIds[0];
        var primarySelection = selectedItems
            .OfType<IEntitySelectable>()
            .FirstOrDefault(selectable => selectable.Entity.Id == primaryEntityId);
        if (primarySelection == null) return;

        _selectionService.SetSelection(selectedItems, primarySelection, sendToEngine: false);
    }
}
