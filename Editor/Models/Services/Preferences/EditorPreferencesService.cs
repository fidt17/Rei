using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Editor.Models.ProjectManagement;
using Editor.Models.Services.Logging;
using Editor.Models.Services.Serialization;
using Editor.Models.Services.Storage;

namespace Editor.Models.Services.Preferences;

public class EditorPreferencesService : IEditorPreferencesService
{
	private const string PREFERENCES_FILE_NAME = "preferences.json";
	
	private EditorPreferences _preferences = null!;
	
	private readonly IEditorStorageService _storageService;
	private readonly ISerializer _serializer;
	private readonly ILogger<EditorPreferencesService> _logger;

	public EditorPreferencesService(
		IEditorStorageService storageService, 
		ILogger<EditorPreferencesService> logger, ISerializer serializer)
	{
		_logger = logger;
		_serializer = serializer;
		_storageService = storageService;
	}

	public async Task InitializeAsync()
	{
		_logger.Log("Initialize");
		_preferences = await LoadPreferences();
	}

	public IEnumerable<Project> GetBookmarkedProjects()
	{
		var validProjects = new List<Project>();
		
		for (var index = _preferences.BookmarkedProjectsPaths.Count - 1; index >= 0; index--)
		{
			var path = _preferences.BookmarkedProjectsPaths[index];
			Project? project = null;
			try
			{
				var data = File.ReadAllText(path);
				project = _serializer.Deserialize<Project>(data);
				project.SetProjectFilePath(path);
			}
			catch (Exception e)
			{
				_logger.LogWarning($"Could not read bookmarked project at path: {path}.\n" +
				                   $"Removing from bookmarks.\n" +
				                   $" Exception: {e.Message}");
				_preferences.BookmarkedProjectsPaths.RemoveAt(index);
			}

			if (project != null)
			{
				validProjects.Add(project);
			}
		}
		
		SavePreferences(_preferences);

		return validProjects;
	}

	public void SetBookmarkedProjects(IEnumerable<Project> paths)
	{
		_preferences.BookmarkedProjectsPaths = paths.Select(x => x.ProjectFilePath).ToList();
		SavePreferences(_preferences);
	}

	private async Task<EditorPreferences> LoadPreferences()
	{
		var preferencesFile = await _storageService.ReadFromFile(PREFERENCES_FILE_NAME);
		if (preferencesFile == null)
		{
			var newPreferences = new EditorPreferences();
			SavePreferences(newPreferences);
			return newPreferences;
		}
		
		return _serializer.Deserialize(preferencesFile, new EditorPreferences());
	}

	private void SavePreferences(EditorPreferences preferences)
	{
		_logger.Log("Save EditorPreferences");
		var data = _serializer.Serialize(preferences);
		_storageService.WriteToFile(PREFERENCES_FILE_NAME, data);
	}
}