using System;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Hierarchies;

public class HierarchyService : IHierarchyService, IDisposable
{
	public Utils.Common.IObservable<Hierarchy?> ActiveHierarchy => _activeHierarchy;

	private readonly Observable<Hierarchy?> _activeHierarchy = new(null);
	private readonly ISceneManagementService _sceneManagementService;
	private readonly IEntityManagementService _entityManagementService;
	
	public HierarchyService(ISceneManagementService sceneManagementService, IEntityManagementService entityManagementService)
	{
		_sceneManagementService = sceneManagementService;
		_entityManagementService = entityManagementService;
		
		_sceneManagementService.CurrentScene.Subscribe(HandleCurrentSceneChangedEvent);
		
		_entityManagementService.EntityCreatedEvent += HandleEntityCreatedEvent;
		_entityManagementService.EntityDeletedEvent += HandleEntityDeletedEvent;
	}

	public void Dispose()
	{
		_sceneManagementService.CurrentScene.Unsubscribe(HandleCurrentSceneChangedEvent);
		
		_entityManagementService.EntityCreatedEvent -= HandleEntityCreatedEvent;
		_entityManagementService.EntityDeletedEvent -= HandleEntityDeletedEvent;
	}

	private void SelectSceneHierarchy(Scene scene) => _activeHierarchy.Value = new Hierarchy(scene);

	private void HandleCurrentSceneChangedEvent(Scene? scene)
	{
		if (scene == null)
		{
			_activeHierarchy.Value = null;
			return;
		}
		
		SelectSceneHierarchy(scene);
	}

	private void HandleEntityCreatedEvent(GameEntity e) => _activeHierarchy.Value?.AddNode(new Hierarchy.Node(e));
	private void HandleEntityDeletedEvent(GameEntity e) => _activeHierarchy.Value?.RemoveNodeWhere(n => n.Entity == e);
}