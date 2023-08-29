using System;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Logging.EditorConsole;

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

		_buildService.BuildStartedEvent += HandleBuildStartedEvent;
		_playmodeService.PlaymodeActiveValueChangedEvent += HandlePlaymodeActiveValueChangedEvent;
	}

	public void Dispose()
	{
		_buildService.BuildStartedEvent -= HandleBuildStartedEvent;
		_playmodeService.PlaymodeActiveValueChangedEvent -= HandlePlaymodeActiveValueChangedEvent;
	}

	private void HandleBuildStartedEvent()
	{
		_editorConsoleService.ClearConsole();
	}

	private void HandlePlaymodeActiveValueChangedEvent(bool isActive)
	{
		_editorConsoleService.ClearConsole();
	}
}