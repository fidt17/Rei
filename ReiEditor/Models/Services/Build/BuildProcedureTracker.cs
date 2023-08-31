using System;
using System.Collections.Generic;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.Services.Build;

public class BuildProcedureTracker : IDisposable
{
	private readonly IBuildService _buildService;
	private readonly IEditorProceduresService _proceduresService;
	private readonly List<Procedure> _activeBuildProcedures = new();

	public BuildProcedureTracker(IBuildService buildService, IEditorProceduresService proceduresService)
	{
		_buildService = buildService;
		_proceduresService = proceduresService;
		
		_buildService.BuildInProgress.Subscribe(HandleBuildInProgressValueChangedEvent);
	}

	public void Dispose()
	{
		_buildService.BuildInProgress.Unsubscribe(HandleBuildInProgressValueChangedEvent);
	}

	private void HandleBuildInProgressValueChangedEvent(bool isBuildInProgress)
	{
		if (isBuildInProgress)
		{
			HandleBuildStartedEvent();
		}
		else
		{
			HandleBuildFinishedEvent();
		}
	}

	private void HandleBuildStartedEvent()
	{
		var procedure = new Procedure("Building project");
		_proceduresService.TrackProcedure(procedure);
		_activeBuildProcedures.Add(procedure);
	}

	private void HandleBuildFinishedEvent()
	{
		_activeBuildProcedures.ForEach(x => x.Complete());
		_activeBuildProcedures.Clear();
	}
}