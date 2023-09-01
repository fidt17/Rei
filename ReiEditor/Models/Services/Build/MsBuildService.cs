using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Build;

public class MsBuildService : IBuildService
{
	public Utils.Common.IObservable<bool> BuildInProgress => _buildInProgress;
	public Utils.Common.IObservable<bool> IsBuildReady => _isBuildReady;

	private readonly Observable<bool> _buildInProgress = new();
	private readonly Observable<bool> _isBuildReady = new();

	private readonly IEditorPreferencesService _editorPreferencesService;
	private readonly IActiveProjectService _activeProjectService;
	private readonly ILogger<MsBuildService> _logger;

	public MsBuildService(IEditorPreferencesService editorPreferencesService, IActiveProjectService activeProjectService, ILogger<MsBuildService> logger)
	{
		_editorPreferencesService = editorPreferencesService;
		_activeProjectService = activeProjectService;
		_logger = logger;
	}

	public async Task<bool> BuildProject(BuildConfigurationEnum configuration)
	{
		if (_buildInProgress)
		{
			_logger.LogError("Another build in progress");
			return false;
		}

		try
		{
			_buildInProgress.Value = true;
			_isBuildReady.Value = false;
			
			var msBuildPath = _editorPreferencesService.GetMsBuildPath();
			if (!File.Exists(msBuildPath)) throw new Exception("Invalid MsBuild path");

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
			_buildInProgress.Value = false;
			
			return true;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		_buildInProgress.Value = false;
		
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
			_isBuildReady.Value = false;
		}
		else
		{
			warnings.ForEach(_logger.LogWarning);
			var warningsCount = warnings.Count;
			_logger.Log($"Build succeeded. Warnings: {warningsCount}");
			_isBuildReady.Value = true;
		}
	}
}