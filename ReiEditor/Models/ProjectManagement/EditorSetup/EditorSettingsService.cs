using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.ProjectManagement.EditorSetup;

public class EditorSettingsService : IEditorSettingsService
{
    public event Action<bool>? EditorConfigurationChangedEvent;
    public event Action? ConfigurationSetEvent;

    private string? _enginePath;
    private string? _msBuildPath;
    private string? _textEditorPath;
	
    private readonly IEditorPreferencesService _preferences;
    private readonly ISerializer _serializer;
    private readonly ILogger<EditorSettingsService> _logger;

    public EditorSettingsService(IEditorPreferencesService preferences, ISerializer serializer, ILogger<EditorSettingsService> logger)
    {
        _preferences = preferences;
        _serializer = serializer;
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        _logger.Log("Initialize");
		
        _enginePath = _preferences.GetEnginePath();
        _msBuildPath = _preferences.GetMsBuildPath();
        _textEditorPath = _preferences.GetTextEditorPath();
		
        return Task.CompletedTask;
    }

    public bool IsEditorConfigurationValid()
    {
        if (!IsEngineLocationValid()) return false;
        if (!IsMsBuildLocationValid()) return false;
		
        return true;
    }

    public void SaveConfiguration()
    {
        if (!IsEditorConfigurationValid()) throw new Exception("Invalid editor configuration");
		
        _preferences.SetEnginePath(_enginePath!);
        _preferences.SetMsBuildPath(_msBuildPath!);
        _preferences.SetTextEditorPath(_textEditorPath ?? "");
		
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

        _logger.LogWarning("Cannot set invalid engine path");
        return false;
    }

    public string GetEngineLocation() => _enginePath ?? "";

    public bool IsMsBuildLocationValid() => _msBuildPath != null && IsMsBuildFileValid(_msBuildPath);

    public bool SetMsBuildLocation(string path)
    {
        if (IsMsBuildFileValid(path))
        {
            _msBuildPath = path;
            InvokeConfigurationChange();
            return true;
        }
		
        _logger.LogWarning("Cannot set invalid MsBuild path");
        return false;
    }

    public string GetMsBuildLocation() => _msBuildPath ?? "";

    public bool IsTextEditorLocationValid()
    {
        if (string.IsNullOrWhiteSpace(_textEditorPath)) return true;
        return IsTextEditorFileValid(_textEditorPath);
    }

    public bool SetTextEditorLocation(string path)
    {
        if (IsTextEditorFileValid(path))
        {
            _textEditorPath = path;
            InvokeConfigurationChange();
            return true;
        }

        _logger.LogWarning("Cannot set invalid text editor path");
        return false;
    }

    public void ClearTextEditorLocation()
    {
        _textEditorPath = "";
        InvokeConfigurationChange();
    }

    public string GetTextEditorLocation() => _textEditorPath ?? "";

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

    private bool IsMsBuildFileValid(string path) => File.Exists(path);

    private static bool IsTextEditorFileValid(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!File.Exists(path)) return false;
        return string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
    }
}