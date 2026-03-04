using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build.ProjectBuild;

public class ProjectBuildService : IProjectBuildService
{
    private static readonly string[] REQUIRED_RESOURCES_FILE_PATTERNS = { "*.bin" };

    private const int TOTAL_STEPS = 5;

    private readonly IBuildStarter _buildStarter;
    private readonly IEditorModeStarter _editorModeStarter;
    private readonly IFileExplorerProvider _fileExplorerProvider;
    private readonly ProjectBuildConfigurationUtility _configurationUtility;
    private readonly ProjectBuildOutputPathUtility _outputPathUtility;
    private readonly IEngineSettingsProvider _engineSettingsProvider;
    private readonly ILogger<ProjectBuildService> _logger;

    public ProjectBuildService(
        IBuildStarter buildStarter,
        IEditorModeStarter editorModeStarter,
        IFileExplorerProvider fileExplorerProvider,
        ProjectBuildConfigurationUtility configurationUtility,
        ProjectBuildOutputPathUtility outputPathUtility,
        IEngineSettingsProvider engineSettingsProvider,
        ILogger<ProjectBuildService> logger)
    {
        _buildStarter = buildStarter;
        _editorModeStarter = editorModeStarter;
        _fileExplorerProvider = fileExplorerProvider;
        _configurationUtility = configurationUtility;
        _outputPathUtility = outputPathUtility;
        _engineSettingsProvider = engineSettingsProvider;
        _logger = logger;
    }

    public async Task<ProjectBuildResult> BuildAsync(
        ProjectBuildRequest request,
        Action<ProjectBuildProgress> progressCallback,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            progressCallback(new ProjectBuildProgress("Preparing build settings", 1, TOTAL_STEPS));
            ValidateRequest(request);
            _configurationUtility.ApplyExecutableBuildSettings(request);
            ValidateEngineArtifacts(request.Configuration);

            cancellationToken.ThrowIfCancellationRequested();
            progressCallback(new ProjectBuildProgress("Rebuilding solution and assets", 2, TOTAL_STEPS));

            var didBuild = await _buildStarter.BuildProject(
                request.Configuration,
                forceSolutionRebuild: true,
                forceCleanSolutionBuild: true,
                cancellationToken);

            if (!didBuild)
            {
                return new ProjectBuildResult(false, false, "Build failed. Check editor console for details.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            progressCallback(new ProjectBuildProgress("Packaging executable", 3, TOTAL_STEPS));
            PackageBuildOutput(request);

            cancellationToken.ThrowIfCancellationRequested();
            progressCallback(new ProjectBuildProgress("Opening output folder", 4, TOTAL_STEPS));
            _fileExplorerProvider.OpenDirectory(request.OutputPath);

            progressCallback(new ProjectBuildProgress("Done", 5, TOTAL_STEPS));
            _editorModeStarter.Start();
            return new ProjectBuildResult(true, false, string.Empty);
        }
        catch (OperationCanceledException)
        {
            return new ProjectBuildResult(false, true, "Build canceled.");
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return new ProjectBuildResult(false, false, e.Message);
        }
    }

    private void ValidateRequest(ProjectBuildRequest request)
    {
        if (request.Configuration is not (BuildConfigurationEnum.Debug or BuildConfigurationEnum.Release))
        {
            throw new Exception("Only Debug and Release executable builds are supported.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new Exception("Output path is required.");
        }
    }

    private void ValidateEngineArtifacts(BuildConfigurationEnum configuration)
    {
        var engineIncludeDir = configuration == BuildConfigurationEnum.Release
            ? _engineSettingsProvider.GetEngineReleaseIncludeDir()
            : _engineSettingsProvider.GetEngineDebugIncludeDir();

        var requiredFiles = new[]
        {
            Path.Combine(engineIncludeDir, "Rei.lib"),
            Path.Combine(engineIncludeDir, "Rei.dll"),
            Path.Combine(engineIncludeDir, "assimp-vc143-mt.dll"),
        };

        var missingFiles = requiredFiles.Where(path => !File.Exists(path)).ToList();
        if (missingFiles.Count == 0) return;

        var missingFilesText = string.Join(Environment.NewLine, missingFiles);
        throw new Exception(
            $"Missing engine build artifacts for {configuration}:{Environment.NewLine}{missingFilesText}");
    }

    private void PackageBuildOutput(ProjectBuildRequest request)
    {
        var buildOutputDirectory = _outputPathUtility.GetBuildOutputDirectory(request.Configuration);
        var buildExePath = _outputPathUtility.GetBuildOutputExePath(request.Configuration);
        var resourcesDirectoryPath = _outputPathUtility.GetResourcesDirectory();

        if (!Directory.Exists(buildOutputDirectory))
        {
            throw new Exception($"Build output folder is missing: {buildOutputDirectory}");
        }

        if (!File.Exists(buildExePath))
        {
            throw new Exception($"Executable was not generated: {buildExePath}");
        }

        var outputDirectory = Path.GetFullPath(request.OutputPath);
        PrepareOutputDirectory(outputDirectory);

        foreach (var filePath in Directory.GetFiles(buildOutputDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(filePath);
            File.Copy(filePath, Path.Combine(outputDirectory, fileName), overwrite: true);
        }

        if (!Directory.Exists(resourcesDirectoryPath))
        {
            throw new Exception($"Resources directory is missing: {resourcesDirectoryPath}");
        }

        CopyRequiredResources(resourcesDirectoryPath, Path.Combine(outputDirectory, "Resources"));
    }

    private static void PrepareOutputDirectory(string outputDirectory)
    {
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);
    }

    private static void CopyRequiredResources(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var filePattern in REQUIRED_RESOURCES_FILE_PATTERNS)
        {
            foreach (var sourcePath in Directory.GetFiles(source, filePattern, SearchOption.TopDirectoryOnly))
            {
                File.Copy(sourcePath, Path.Combine(target, Path.GetFileName(sourcePath)), overwrite: true);
            }
        }
    }
}
