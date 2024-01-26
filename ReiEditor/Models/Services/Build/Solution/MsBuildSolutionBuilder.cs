using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Build.Solution;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;

namespace ReiEditor.Models.Services.Build;

public class MsBuildSolutionBuilder : ISolutionBuilder
{
    private readonly IResourceService _resourceService;
    private readonly IEditorPreferencesService _editorPreferencesService;
    private readonly IActiveProjectService _activeProjectService;
    private readonly ILogger<MsBuildSolutionBuilder> _logger;

    public MsBuildSolutionBuilder(IResourceService resourceService, IEditorPreferencesService editorPreferencesService, IActiveProjectService activeProjectService, ILogger<MsBuildSolutionBuilder> logger)
    {
        _resourceService = resourceService;
        _editorPreferencesService = editorPreferencesService;
        _activeProjectService = activeProjectService;
        _logger = logger;
    }

    public async Task Build(BuildConfigurationEnum configuration)
    {
        var msBuildPath = _editorPreferencesService.GetMsBuildPath();
        if (!File.Exists(msBuildPath)) throw new Exception("Invalid MsBuild path");
		
        var msBuildProcess = new Process();
        msBuildProcess.StartInfo.FileName = msBuildPath;
        msBuildProcess.StartInfo.Arguments = $"\"{_resourceService.GetRootPath()}\" -v:q /t:Clean;Build /p:Configuration={configuration}";
        msBuildProcess.StartInfo.CreateNoWindow = true;
        msBuildProcess.StartInfo.RedirectStandardOutput = true;
			
        msBuildProcess.Start();
        string output = await msBuildProcess.StandardOutput.ReadToEndAsync();
        await msBuildProcess.WaitForExitAsync();

        ParseMsBuildOutput(output);
    }

    private void ParseMsBuildOutput(string output)
    {
        var project = _activeProjectService.GetActiveProject();
        var projectDirPath = Path.GetDirectoryName(_resourceService.GetSolutionPath()) + "\\Scripts\\";

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
            throw new Exception($"Build errors: {errorsCount}");
        }

        warnings.ForEach(_logger.LogWarning);
    }
}