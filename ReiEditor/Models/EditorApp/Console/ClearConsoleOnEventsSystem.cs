using System;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.EditorApp.Console;

public class ClearConsoleOnEventsSystem : IDisposable
{
    private readonly IEditorConsoleService _editorConsoleService;
    private readonly IBuildService _buildService;
    private readonly IEngineRunner _engineRunner;

    public ClearConsoleOnEventsSystem(IBuildService buildService, IEditorConsoleService editorConsoleService, IEngineRunner engineRunner)
    {
        _buildService = buildService;
        _editorConsoleService = editorConsoleService;
        _engineRunner = engineRunner;

        _buildService.BuildInProgress.Subscribe(HandleBuildInProgressValueChangedEvent);
        _engineRunner.IsEngineStarting.Subscribe(HandleEngineStartingValueChangedEvent);
    }

    public void Dispose()
    {
        _buildService.BuildInProgress.Unsubscribe(HandleBuildInProgressValueChangedEvent);
        _engineRunner.IsEngineStarting.Unsubscribe(HandleEngineStartingValueChangedEvent);
    }

    private void HandleBuildInProgressValueChangedEvent(bool isBuildInProgress)
    {
        if (isBuildInProgress)
        {
            _editorConsoleService.ClearConsole();
        }
    }

    private void HandleEngineStartingValueChangedEvent(bool isStarting)
    {
        if (isStarting && _engineRunner.ActiveMode == EngineRunMode.PlayMode)
        {
            _editorConsoleService.ClearConsole();
        }
    }
}