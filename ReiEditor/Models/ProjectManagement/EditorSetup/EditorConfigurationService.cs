using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.ProjectManagement.EditorSetup;

public class EditorConfigurationService : IEditorConfigurationService
{
	public event Action<bool>? EditorConfigurationChangedEvent;
	public event Action? ConfigurationSetEvent;

	private string? _enginePath;
	
	private readonly IEditorPreferencesService _preferences;
	private readonly ISerializer _serializer;
	private readonly ILogger<EditorConfigurationService> _logger;

	public EditorConfigurationService(IEditorPreferencesService preferences, ISerializer serializer, ILogger<EditorConfigurationService> logger)
	{
		_preferences = preferences;
		_serializer = serializer;
		_logger = logger;
	}

	public Task InitializeAsync()
	{
		_logger.Log("Initialize");
		_enginePath = _preferences.GetEnginePath();
		return Task.CompletedTask;
	}

	public bool IsEditorConfigurationValid() => IsEngineLocationValid();
	
	public void SaveConfiguration()
	{
		if (!IsEditorConfigurationValid()) throw new Exception("Invalid editor configuration");
		
		if (string.IsNullOrWhiteSpace(_enginePath)) return;
		_logger.Log($"Set engine path: {_enginePath}");
		_preferences.SetEnginePath(_enginePath);
		ConfigurationSetEvent?.Invoke();
	}

	public bool IsEngineLocationValid() => _enginePath != null && IsEngineFileValid(_enginePath);

	public bool SetEngineLocation(string path)
	{
		if (IsEngineFileValid(path))
		{
			_enginePath = path;
			InvokeConfigurationChange();
			return true;
		}

		_logger.Log("Cannot set invalid engine path");
		return false;
	}

	private void InvokeConfigurationChange()
	{
		EditorConfigurationChangedEvent?.Invoke(IsEditorConfigurationValid());
	}

	private bool IsEngineFileValid(string path)
	{
		try
		{
			var engineSettings = _serializer.Deserialize<EngineSettings>(File.ReadAllText(path));
			return engineSettings != null;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return false;
	}
}