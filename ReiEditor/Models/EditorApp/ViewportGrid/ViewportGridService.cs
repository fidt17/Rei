using System;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Preferences;

namespace ReiEditor.Models.EditorApp.ViewportGrid;

public class ViewportGridService : IViewportGridService, IDisposable
{
    private readonly ViewportGridSettings _settings;
    
    private readonly IEditorPreferencesService _preferencesService;
    private readonly IEngineApi _engineApi;
    private readonly IEngineRunner _engineRunner;

    public ViewportGridService(IEditorPreferencesService preferencesService, IEngineApi engineApi, IEngineRunner engineRunner)
    {
        _preferencesService = preferencesService;
        _engineApi = engineApi;
        _engineRunner = engineRunner;

        _settings = _preferencesService.GetGridSettings();
        
        _engineRunner.EngineStartedEvent += HandleEngineStartedEvent;
    }

    public void Dispose()
    {
        _engineRunner.EngineStartedEvent -= HandleEngineStartedEvent;
        _preferencesService.SetGridSettings(_settings);
    }

    public ViewportGridSettings GetCurrentSettings()
    {
        return _settings;
    }

    public void EnableXZGrid(bool value)
    {
        if (_settings.RenderXZ == value) return;
        
        _settings.RenderXZ = value;
        _settings.RenderXY = false;
        _settings.RenderYZ = false;
        
        SendSettingsToEngine();
    }
    
    public void EnableXYGrid(bool value)
    {
        if (_settings.RenderXY == value) return;
        
        _settings.RenderXZ = false;
        _settings.RenderXY = value;
        _settings.RenderYZ = false;
        
        SendSettingsToEngine();
    }

    public void EnableYZGrid(bool value)
    {
        if (_settings.RenderYZ == value) return;
        
        _settings.RenderXZ = false;
        _settings.RenderXY = false;
        _settings.RenderYZ = value;
        
        SendSettingsToEngine();
    }

    public void SetOpacity(float value)
    {
        _settings.Opacity = value;

        SendSettingsToEngine();
    }

    private void SendSettingsToEngine()
    {
        _engineApi.SetEditorGridSettings(new SetViewportGridSettingsRequest(_settings));
    }
    
    private void HandleEngineStartedEvent()
    {
        SendSettingsToEngine();
    }
}