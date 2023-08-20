using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Engine;

public class EngineSettingsProvider : IEngineSettingsProvider, IDisposable
{
	private EngineSettings _engineSettings = null!;
	private string _enginePath = null!;
	
	private readonly IEditorPreferencesService _preferences;
	private readonly ISerializer _serializer;
	private readonly ILogger<EngineSettingsProvider> _logger;
	private readonly IEditorConfigurationService _editorConfigurationService;

	public EngineSettingsProvider(IEditorPreferencesService preferences, ISerializer serializer, ILogger<EngineSettingsProvider> logger, 
		IEditorConfigurationService editorConfigurationService)
	{
		_preferences = preferences;
		_serializer = serializer;
		_logger = logger;
		_editorConfigurationService = editorConfigurationService;
		_editorConfigurationService.ConfigurationSetEvent += HandleEditorConfigurationSetEvent;
	}

	public Task InitializeAsync()
	{
		_logger.Log("Initialize");
		if (!_editorConfigurationService.IsEditorConfigurationValid()) return Task.CompletedTask;
		
		LoadEngineSettings();
		
		return Task.CompletedTask;
	}
	
	public void Dispose()
	{
		_editorConfigurationService.ConfigurationSetEvent -= HandleEditorConfigurationSetEvent;
	}

	public string GetEngineDebugIncludeDir() => _enginePath + _engineSettings.RelativeDebugIncludeDir;
	public string GetEngineReleaseIncludeDir() => _enginePath + _engineSettings.RelativeReleaseIncludeDir;
	public string GetEngineSourceIncludeDir() => _enginePath + _engineSettings.RelativeSourceIncludeDir;

	private void HandleEditorConfigurationSetEvent() => LoadEngineSettings();

	private void LoadEngineSettings()
	{
		_logger.Log("Load engine settings");
		
		try
		{
			var filePath = _preferences.GetEnginePath() ?? throw new Exception("Missing engine file");
			var file = File.ReadAllText(filePath);
		
			_engineSettings = _serializer.Deserialize<EngineSettings>(file);
			if (_engineSettings == null) throw new Exception("Could not deserialize engine settings file");
			_enginePath = Path.GetDirectoryName(filePath) ?? throw new Exception("Could not get engine directory path");
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
	}
}