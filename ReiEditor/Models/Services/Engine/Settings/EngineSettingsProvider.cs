using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Engine.Settings;

public class EngineSettingsProvider : IEngineSettingsProvider, IDisposable
{
    private EngineSettings _engineSettings = null!;
    private string _enginePath = null!;
	
    private readonly IEditorPreferencesService _preferences;
    private readonly ISerializer _serializer;
    private readonly ILogger<EngineSettingsProvider> _logger;
    private readonly IEditorSettingsService _editorSettingsService;

    public EngineSettingsProvider(IEditorPreferencesService preferences, ISerializer serializer, ILogger<EngineSettingsProvider> logger, 
        IEditorSettingsService editorSettingsService)
    {
        _preferences = preferences;
        _serializer = serializer;
        _logger = logger;
        _editorSettingsService = editorSettingsService;
        _editorSettingsService.ConfigurationSetEvent += HandleEditorSettingsSetEvent;
    }

    public Task InitializeAsync()
    {
        _logger.Log("Initialize");
        if (!_editorSettingsService.IsEditorConfigurationValid()) return Task.CompletedTask;
		
        LoadEngineSettings();
		
        return Task.CompletedTask;
    }
	
    public void Dispose()
    {
        _editorSettingsService.ConfigurationSetEvent -= HandleEditorSettingsSetEvent;
    }

    public string GetEnginePath() => _enginePath;

    public string GetEngineDebugIncludeDir() => _enginePath + _engineSettings.RelativeDebugIncludeDir;
    public string GetEngineReleaseIncludeDir() => _enginePath + _engineSettings.RelativeReleaseIncludeDir;
    public string GetEngineSourceIncludes()
    {
        var includes = _engineSettings.RelativeSourceIncludes.Split(';');
        return includes.Aggregate("", (current, include) => current + ";" + (_enginePath + include));
    }

    public string GetEngineResourcesDir() => _enginePath + _engineSettings.RelativeResourcesDir;
    public string GetEngineBehavioursDir() => GetEngineResourcesDir() + "\\rei_behaviours";

    private void HandleEditorSettingsSetEvent() => LoadEngineSettings();

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