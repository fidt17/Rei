using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;

namespace ReiEditor.Models.Services.Build.Solution;

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

    public async Task Build(
        BuildConfigurationEnum configuration,
        bool cleanBuild = false,
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        _logger.Log($"Building solution. Configuration: {configuration}");
        
        var msBuildPath = _editorPreferencesService.GetMsBuildPath();
        if (!File.Exists(msBuildPath)) throw new Exception("Invalid MsBuild path");

        using var msBuildProcess = new Process();
        msBuildProcess.StartInfo.FileName = msBuildPath;
        var buildTarget = cleanBuild ? "Clean;Build" : "Build";

        msBuildProcess.StartInfo.Arguments = MsBuildArgumentsBuilder.Build(_resourceService.GetRootPath(), configuration, buildTarget, outputDirectory);
        msBuildProcess.StartInfo.UseShellExecute = false;
        msBuildProcess.StartInfo.CreateNoWindow = true;
        msBuildProcess.StartInfo.RedirectStandardError = true;
        msBuildProcess.StartInfo.RedirectStandardOutput = true;
			
        msBuildProcess.Start();
        using var _ = cancellationToken.Register(() =>
        {
            try
            {
                if (!msBuildProcess.HasExited)
                {
                    msBuildProcess.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignored
            }
        });
        var errorOutput = msBuildProcess.StandardError.ReadToEndAsync();
        string output = await msBuildProcess.StandardOutput.ReadToEndAsync();
        await msBuildProcess.WaitForExitAsync();

        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.Log($"Solution build finished");

        ParseMsBuildOutput(output + Environment.NewLine + await errorOutput);
        if (msBuildProcess.ExitCode != 0) throw new Exception($"MSBuild failed with exit code {msBuildProcess.ExitCode}.");
    }

    private void ParseMsBuildOutput(string output)
    {
        var project = _activeProjectService.GetActiveProject();
        var projectDirPath = Path.GetDirectoryName(_resourceService.GetScriptsPath()) + "\\Scripts\\";

        var warnings = new List<string>();
        var errors = new List<string>();
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            string cleanUpString(string str) => str.Replace(projectDirPath, "").Replace($"[{project.ProjectName}.vcxproj]", "");
			
            if (Regex.IsMatch(line, @"\bwarning\s+[A-Z]+\d+\s*:", RegexOptions.IgnoreCase))
            {
                warnings.Add(cleanUpString(line));
            }
            else if (Regex.IsMatch(line, @"\berror\s+[A-Z]+\d+\s*:", RegexOptions.IgnoreCase))
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
