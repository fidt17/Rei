using System;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Hierarchies;

public class HierarchyService : IHierarchyService, IDisposable
{
	public Utils.Common.IObservable<Hierarchy?> ActiveHierarchy => _activeHierarchy;

	private readonly Observable<Hierarchy?> _activeHierarchy = new(null);
	private readonly ISceneManagementService _sceneManagementService;
	
	public HierarchyService(ISceneManagementService sceneManagementService)
	{
		_sceneManagementService = sceneManagementService;
		_sceneManagementService.CurrentScene.Subscribe(HandleCurrentSceneChangedEvent);
	}

	public void Dispose()
	{
		_sceneManagementService.CurrentScene.Unsubscribe(HandleCurrentSceneChangedEvent);
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
}