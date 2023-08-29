using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;

namespace ReiEditor.Models.Services.Build;

public class MsBuildService : IBuildService, IDisposable
{
	public event Action<bool>? CanStartBuildChangedEvent;
	public event Action? BuildStartedEvent;
	public event Action? BuildFinishedEvent;

	private bool _canStartBuild;
	public bool CanStartBuild
	{
		get => _canStartBuild;
		private set
		{
			if (value == CanStartBuild) return;
			_canStartBuild = value;
			CanStartBuildChangedEvent?.Invoke(CanStartBuild);
		}
	}

	public bool BuildInProgress { get; private set; }

	private readonly IEditorPreferencesService _editorPreferencesService;
	private readonly IActiveProjectService _activeProjectService;
	private readonly ILogger<MsBuildService> _logger;
	private readonly IClientDllManager _dllManager;
	private readonly IPlaymodeService _playmodeService;

	public MsBuildService(IEditorPreferencesService editorPreferencesService, IActiveProjectService activeProjectService, ILogger<MsBuildService> logger, IClientDllManager dllManager, IPlaymodeService playmodeService)
	{
		_editorPreferencesService = editorPreferencesService;
		_activeProjectService = activeProjectService;
		_logger = logger;
		_dllManager = dllManager;
		_playmodeService = playmodeService;
		
		_playmodeService.PlaymodeActiveValueChangedEvent += HandlePlaymodeActiveValueChangedEvent;
		
		UpdateCanStartBuild();
	}

	public void Dispose()
	{
		_playmodeService.PlaymodeActiveValueChangedEvent -= HandlePlaymodeActiveValueChangedEvent;
	}

	private void HandlePlaymodeActiveValueChangedEvent(bool isActive) => UpdateCanStartBuild();

	private void UpdateCanStartBuild()
	{
		if (_playmodeService.PlaymodeActive)
		{
			CanStartBuild = false;
			return;
		}

		if (BuildInProgress)
		{
			CanStartBuild = false;
			return;
		}

		CanStartBuild = true;
	}

	public async Task<bool> BuildProject(BuildConfigurationEnum configuration)
	{
		try
		{
			if (!CanStartBuild) throw new Exception("Cannot start build process at the moment");
			BuildInProgress = true;
			CanStartBuild = false;
			BuildStartedEvent?.Invoke();
			
			var msBuildPath = _editorPreferencesService.GetMsBuildPath();
			if (!File.Exists(msBuildPath)) throw new Exception("Invalid MsBuild path");

			if (_dllManager.DllLoaded())
			{
				_dllManager.UnloadDll();
			}

			var project = _activeProjectService.GetActiveProject();
		
			var msBuildProcess = new Process();
			msBuildProcess.StartInfo.FileName = msBuildPath;
			msBuildProcess.StartInfo.Arguments = $"\"{project.ProjectSolutionPath}\" -v:q /t:Clean;Build /p:Configuration={configuration}";
			msBuildProcess.StartInfo.CreateNoWindow = true;
			msBuildProcess.StartInfo.RedirectStandardOutput = true;
			
			msBuildProcess.Start();
			string output = await msBuildProcess.StandardOutput.ReadToEndAsync();
			await msBuildProcess.WaitForExitAsync();

			ParseMsBuildOutput(output);
			
			BuildInProgress = false;
			UpdateCanStartBuild();
			BuildFinishedEvent?.Invoke();
			
			return true;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		BuildInProgress = false;
		UpdateCanStartBuild();
		BuildFinishedEvent?.Invoke();
		
		return false;
	}

	private void ParseMsBuildOutput(string output)
	{
		var project = _activeProjectService.GetActiveProject();
		var projectDirPath = Path.GetDirectoryName(project.ProjectSolutionPath) + "\\Scripts\\";

		var warnings = new List<string>();
		var errors = new List<string>();
		var lines = output.Split('\n');
		foreach (var line in lines)
		{
			string cleanUpString(string str) => str.Replace(projectDirPath, "").Replace($"[{project.ProjectName}.vcxproj]", "");
			
			if (line.Contains("warning"))
			{
				warnings.Add(cleanUpString(line));
			}
			else if (line.Contains("error"))
			{
				errors.Add(cleanUpString(line));
			}
		}

		var errorsCount = errors.Count;
		if (errorsCount > 0)
		{
			errors.ForEach(_logger.LogError);
			_logger.LogError($"Build failed. Errors: {errorsCount}");
		}
		else
		{
			warnings.ForEach(_logger.LogWarning);
			var warningsCount = warnings.Count;
			_logger.Log($"Build succeeded. Warnings: {warningsCount}");
		}
	}
}