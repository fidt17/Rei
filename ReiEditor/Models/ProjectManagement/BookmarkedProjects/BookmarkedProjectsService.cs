using System;
using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;

namespace ReiEditor.Models.ProjectManagement.BookmarkedProjects;

public class BookmarkedProjectsService : IBookmarkedProjectsService
{
	public event Action? BookmarkedProjectsCollectionChangedEvent;
	
	private readonly IEditorPreferencesService _editorPreferencesService;
	private readonly ILogger<BookmarkedProjectsService> _logger;
	
	private readonly List<Project> _projects;

	public BookmarkedProjectsService(IEditorPreferencesService editorPreferencesService, ILogger<BookmarkedProjectsService> logger)
	{
		_logger = logger;
		
		_editorPreferencesService = editorPreferencesService;
		_projects = editorPreferencesService.GetBookmarkedProjects().ToList();
	}

	public IEnumerable<Project> GetBookmarkedProjects() => _projects;

	public void AddProject(Project project)
	{
		if (_projects.Exists(x => x.Equals(project)))
		{
			_logger.LogWarning($"Cannot add project [{project.ProjectName}] because it does already exist in bookmarked projects collection.");
			return;
		}
		
		_projects.Add(project);
		_editorPreferencesService.SetBookmarkedProjects(_projects);
		
		BookmarkedProjectsCollectionChangedEvent?.Invoke();
	}

	public void RemoveProject(Project project)
	{
		if (!_projects.Contains(project))
		{
			_logger.LogWarning($"Cannot remove project [{project}] because it does not exist in bookmarked projects collection.");
			return;
		}
		
		_projects.Remove(project);
		_editorPreferencesService.SetBookmarkedProjects(_projects);
		
		BookmarkedProjectsCollectionChangedEvent?.Invoke();
	}
}