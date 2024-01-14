using System;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.EditorApp.Console;

public class ClearConsoleOnEventsSystem : IDisposable
{
	private readonly IEditorConsoleService _editorConsoleService;
	private readonly IBuildService _buildService;
	private readonly IPlaymodeService _playmodeService;

	public ClearConsoleOnEventsSystem(IBuildService buildService, IEditorConsoleService editorConsoleService, IPlaymodeService playmodeService)
	{
		_buildService = buildService;
		_editorConsoleService = editorConsoleService;
		_playmodeService = playmodeService;

		_buildService.BuildInProgress.Subscribe(HandleBuildInProgressValueChangedEvent);
		_playmodeService.IsPlaymodeActive.Subscribe(HandlePlaymodeActiveValueChangedEvent);
	}

	public void Dispose()
	{
		_buildService.BuildInProgress.Unsubscribe(HandleBuildInProgressValueChangedEvent);
		_playmodeService.IsPlaymodeActive.Unsubscribe(HandlePlaymodeActiveValueChangedEvent);
	}

	private void HandleBuildInProgressValueChangedEvent(bool isBuildInProgress)
	{
		if (isBuildInProgress)
		{
			_editorConsoleService.ClearConsole();
		}
	}

	private void HandlePlaymodeActiveValueChangedEvent(bool isActive)
	{
		if (isActive)
		{
			_editorConsoleService.ClearConsole();
		}
	}
}