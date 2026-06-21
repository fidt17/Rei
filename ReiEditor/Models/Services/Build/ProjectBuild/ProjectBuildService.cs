using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build.ProjectBuild;

public class ProjectBuildService : IProjectBuildService
{
    private sealed record BuildStep(
        string Status,
        Func<CancellationToken, int, int, Task<ProjectBuildResult?>> ExecuteAsync);

    private const string BUILD_VERSION = "0.0.1";
    private static readonly string[] REQUIRED_RESOURCES_FILE_PATTERNS = { "*.bin" };

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
        var stopwatch = Stopwatch.StartNew();

        void PostProgress(ProjectBuildProgress p)
        {
            _logger.Log(p.Status);
            progressCallback(p);
        }

        var steps = CreateBuildSteps(request, stopwatch, PostProgress);
        var totalSteps = steps.Count;

        try
        {
            for (var i = 0; i < totalSteps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stepNumber = i + 1;
                PostProgress(new ProjectBuildProgress(steps[i].Status, stepNumber, totalSteps));
                var result = await steps[i].ExecuteAsync(cancellationToken, stepNumber, totalSteps);
                if (result.HasValue)
                {
                    return result.Value;
                }
            }

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
        finally
        {
            stopwatch.Stop();
        }
    }

    private List<BuildStep> CreateBuildSteps(ProjectBuildRequest request, Stopwatch stopwatch, Action<ProjectBuildProgress> postProgress)
    {
        return new List<BuildStep>
        {
            new(
                "Preparing build settings",
                (ct, _, _) => PrepareBuildSettingsStepAsync(request, ct)),
            new(
                "Building solution",
                (ct, _, _) => BuildSolutionStepAsync(request, ct)),
            new(
                "Building assets",
                (ct, currentStep, totalSteps) => BuildAssetsStepAsync(request, ct, currentStep, totalSteps, postProgress)),
            new(
                "Packaging executable",
                (ct, _, _) => PackageExecutableStepAsync(request, stopwatch, ct)),
            new(
                "Opening output folder",
                (ct, _, _) => OpenOutputFolderStepAsync(request, ct)),
            new(
                "Done",
                (ct, _, _) => FinalizeStepAsync(ct)),
        };
    }

    private async Task<ProjectBuildResult?> PrepareBuildSettingsStepAsync(ProjectBuildRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        _configurationUtility.ApplyExecutableBuildSettings(request);
        ValidateEngineArtifacts(request.Configuration);

        // delay for a better visual experience
        await Task.Delay(1000, cancellationToken);
        return null;
    }

    private async Task<ProjectBuildResult?> BuildSolutionStepAsync(
        ProjectBuildRequest request,
        CancellationToken cancellationToken)
    {
        var didBuild = await _buildStarter.BuildProject(
            request.Configuration,
            forceSolutionRebuild: true,
            forceCleanSolutionBuild: true,
            buildSolution: true,
            buildAssets: false,
            cancellationToken: cancellationToken);

        return didBuild ? null : new ProjectBuildResult(false, false, "Build failed. Check editor console for details.");
    }

    private async Task<ProjectBuildResult?> BuildAssetsStepAsync(
        ProjectBuildRequest request,
        CancellationToken cancellationToken,
        int currentStep,
        int totalSteps,
        Action<ProjectBuildProgress> postProgress)
    {
        void ReportAssetBuilding(AssetBuildProgressInfo progress)
        {
            var assetName = Path.GetFileName(progress.AssetPath);
            var label = string.IsNullOrWhiteSpace(assetName) ? progress.AssetPath : assetName;
            postProgress(new ProjectBuildProgress($"Building asset {progress.CurrentAssetIndex}/{progress.TotalAssets}: {label}", currentStep, totalSteps));
        }

        var didBuild = await _buildStarter.BuildProject(
            request.Configuration,
            forceAssetRebuild: true,
            buildSolution: false,
            buildAssets: true,
            onAssetBuilding: ReportAssetBuilding,
            cancellationToken: cancellationToken);

        return didBuild ? null : new ProjectBuildResult(false, false, "Build failed. Check editor console for details.");
    }

    private Task<ProjectBuildResult?> PackageExecutableStepAsync(
        ProjectBuildRequest request,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PackageBuildOutput(request);
        WriteBuildResultFile(request, stopwatch.Elapsed);
        return Task.FromResult<ProjectBuildResult?>(null);
    }

    private Task<ProjectBuildResult?> OpenOutputFolderStepAsync(ProjectBuildRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _fileExplorerProvider.OpenDirectory(request.OutputPath);
        return Task.FromResult<ProjectBuildResult?>(null);
    }

    private Task<ProjectBuildResult?> FinalizeStepAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _editorModeStarter.Start();
        return Task.FromResult<ProjectBuildResult?>(null);
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

    private void WriteBuildResultFile(ProjectBuildRequest request, TimeSpan elapsed)
    {
        var outputDirectory = Path.GetFullPath(request.OutputPath);
        var resourcesDirectory = Path.Combine(outputDirectory, "Resources");
        var reportFilePath = Path.Combine(outputDirectory, "BuildResult_DO_NOT_SHIP.txt");
        var sourceResourcesDirectoryPath = _outputPathUtility.GetResourcesDirectory();
        var sourceMapJsonPath = Path.Combine(sourceResourcesDirectoryPath, "map.json");
        var packagedAssetsBinPath = Path.Combine(resourcesDirectory, "assets.bin");
        var assetEntries = GetAssetEntriesWithSizes(sourceMapJsonPath, packagedAssetsBinPath);

        var report = new StringBuilder();
        report.AppendLine("REI BUILD RESULT");
        report.AppendLine("================");
        report.AppendLine($"Build Version: {BUILD_VERSION}");
        report.AppendLine($"Timestamp (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Configuration: {request.Configuration}");
        report.AppendLine($"Console: {(request.ShowConsole ? "Enabled" : "Disabled")}");
        report.AppendLine($"Output Directory: {outputDirectory}");
        report.AppendLine($"Build Duration: {elapsed.TotalSeconds:0.00} seconds");
        report.AppendLine();

        report.AppendLine("Packaged Files:");
        foreach (var file in Directory.GetFiles(outputDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var fileInfo = new FileInfo(file);
            report.AppendLine($"- {Path.GetFileName(file)} | {FormatBytes(fileInfo.Length)}");
        }
        report.AppendLine();

        report.AppendLine("Packaged Resources:");
        if (Directory.Exists(resourcesDirectory))
        {
            foreach (var file in Directory.GetFiles(resourcesDirectory, "*.bin", SearchOption.TopDirectoryOnly))
            {
                var fileInfo = new FileInfo(file);
                report.AppendLine($"- Resources\\{Path.GetFileName(file)} | {FormatBytes(fileInfo.Length)}");
            }
        }
        else
        {
            report.AppendLine("- (missing Resources directory)");
        }
        report.AppendLine();

        report.AppendLine("Assets Summary:");
        foreach (var line in BuildAssetSummaryLines(assetEntries))
        {
            report.AppendLine(line);
        }
        report.AppendLine();

        report.AppendLine("Built Scenes:");
        foreach (var line in BuildScenesSummaryLines(assetEntries))
        {
            report.AppendLine(line);
        }
        report.AppendLine();

        report.AppendLine("Notes:");
        report.AppendLine("- Cache and crash_reports are intentionally excluded from packaged output.");
        report.AppendLine("- crash_reports shipping support can be added later.");

        File.WriteAllText(reportFilePath, report.ToString());
    }

    private static IEnumerable<string> BuildAssetSummaryLines(IReadOnlyList<BuildAssetEntry> assetEntries)
    {
        if (assetEntries.Count == 0)
        {
            return new[] { "- No assets found in map.json." };
        }

        var lines = new List<string> { $"- Total assets: {assetEntries.Count}" };

        lines.Add("- Assets by extension:");
        var groupedByExtension = assetEntries
            .GroupBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var extensionGroup in groupedByExtension)
        {
            var extensionLabel = string.IsNullOrWhiteSpace(extensionGroup.Key) ? "(no extension)" : extensionGroup.Key;
            lines.Add($"  - {extensionLabel}: {extensionGroup.Count()}");
        }

        lines.Add("- Assets:");
        foreach (var asset in assetEntries.OrderByDescending(x => x.Size))
        {
            lines.Add($"  - {FormatBytes(asset.Size)} | {asset.Name} | {asset.ProjectRelativePath}");
        }

        return lines;
    }

    private static IEnumerable<string> BuildScenesSummaryLines(IReadOnlyList<BuildAssetEntry> assetEntries)
    {
        var scenes = assetEntries
            .Where(x => string.Equals(x.Extension, ".scene", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.ProjectRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (scenes.Count == 0) return new[] { "- No scene assets found." };

        var lines = new List<string> { $"- Scenes built: {scenes.Count}" };
        foreach (var scene in scenes)
        {
            lines.Add($"  - {scene.ProjectRelativePath}");
        }

        return lines;
    }

    private static IReadOnlyList<BuildAssetEntry> GetAssetEntriesWithSizes(string mapJsonPath, string assetsBinPath)
    {
        if (!File.Exists(mapJsonPath) || !File.Exists(assetsBinPath))
        {
            return Array.Empty<BuildAssetEntry>();
        }

        var mapJson = File.ReadAllText(mapJsonPath);
        var root = JObject.Parse(mapJson);
        var assets = root["Assets"] as JArray;
        if (assets == null || assets.Count == 0)
        {
            return Array.Empty<BuildAssetEntry>();
        }

        var entries = new List<(string Name, string AssetPath, string ProjectRelativePath, string Extension, long Offset)>();
        foreach (var item in assets.OfType<JObject>())
        {
            var offsetToken = item["Offset"];
            if (offsetToken == null || !long.TryParse(offsetToken.ToString(), out var offset)) continue;

            var name = item["Name"]?.ToString() ?? "unknown";
            var path = item["AssetPath"]?.ToString() ?? "unknown";
            var projectRelativePath = GetProjectRelativePath(path);
            var extension = Path.GetExtension(path);

            entries.Add((name, path, projectRelativePath, extension, offset));
        }

        if (entries.Count == 0)
        {
            return Array.Empty<BuildAssetEntry>();
        }

        entries.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        var assetsBinSize = new FileInfo(assetsBinPath).Length;
        var result = new List<BuildAssetEntry>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var current = entries[i];
            var nextOffset = i + 1 < entries.Count ? entries[i + 1].Offset : assetsBinSize;
            var size = Math.Max(0, nextOffset - current.Offset);
            result.Add(new BuildAssetEntry(current.Name, current.AssetPath, current.ProjectRelativePath, current.Extension, size));
        }

        return result;
    }

    private static string GetProjectRelativePath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return "unknown";

        var normalizedPath = assetPath.Replace('/', '\\');
        const string PROJECT_SEGMENT = "\\Project\\";
        var projectSegmentIndex = normalizedPath.IndexOf(PROJECT_SEGMENT, StringComparison.OrdinalIgnoreCase);
        if (projectSegmentIndex < 0) return normalizedPath;

        return normalizedPath[(projectSegmentIndex + PROJECT_SEGMENT.Length)..];
    }

    private static string FormatBytes(long bytes)
    {
        var value = bytes;
        var suffixes = new[] { "B", "KB", "MB", "GB" };
        var i = 0;
        double readable = value;
        while (readable >= 1024 && i < suffixes.Length - 1)
        {
            readable /= 1024;
            i++;
        }

        return $"{readable:0.##} {suffixes[i]}";
    }

    private readonly record struct BuildAssetEntry(
        string Name,
        string AssetPath,
        string ProjectRelativePath,
        string Extension,
        long Size);
}
