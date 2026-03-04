using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
        var stopwatch = Stopwatch.StartNew();

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
            WriteBuildResultFile(request, stopwatch.Elapsed);

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
        finally
        {
            stopwatch.Stop();
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

    private void WriteBuildResultFile(ProjectBuildRequest request, TimeSpan elapsed)
    {
        var outputDirectory = Path.GetFullPath(request.OutputPath);
        var resourcesDirectory = Path.Combine(outputDirectory, "Resources");
        var reportFilePath = Path.Combine(outputDirectory, "BuildResult_DO_NOT_SHIP.txt");

        var report = new StringBuilder();
        report.AppendLine("REI BUILD RESULT");
        report.AppendLine("================");
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

        report.AppendLine("Asset Payload Summary:");
        var sourceResourcesDirectoryPath = _outputPathUtility.GetResourcesDirectory();
        var sourceMapJsonPath = Path.Combine(sourceResourcesDirectoryPath, "map.json");
        var packagedAssetsBinPath = Path.Combine(resourcesDirectory, "assets.bin");
        foreach (var line in BuildAssetSummaryLines(sourceMapJsonPath, packagedAssetsBinPath))
        {
            report.AppendLine(line);
        }
        report.AppendLine();

        report.AppendLine("Notes:");
        report.AppendLine("- Cache and crash_reports are intentionally excluded from packaged output.");
        report.AppendLine("- crash_reports shipping support can be added later.");

        File.WriteAllText(reportFilePath, report.ToString());
    }

    private static IEnumerable<string> BuildAssetSummaryLines(string mapJsonPath, string assetsBinPath)
    {
        if (!File.Exists(mapJsonPath))
        {
            return new[] { "- map.json not found. Asset breakdown unavailable." };
        }

        if (!File.Exists(assetsBinPath))
        {
            return new[] { "- assets.bin not found. Asset breakdown unavailable." };
        }

        var mapJson = File.ReadAllText(mapJsonPath);
        var root = JObject.Parse(mapJson);
        var assets = root["Assets"] as JArray;
        if (assets == null || assets.Count == 0)
        {
            return new[] { "- No assets found in map.json." };
        }

        var assetEntries = new List<(string name, string path, long offset)>();
        foreach (var item in assets.OfType<JObject>())
        {
            var offsetToken = item["Offset"];
            if (offsetToken == null) continue;
            if (!long.TryParse(offsetToken.ToString(), out var offset)) continue;

            var name = item["Name"]?.ToString() ?? "unknown";
            var path = item["AssetPath"]?.ToString() ?? "unknown";
            assetEntries.Add((name, path, offset));
        }

        if (assetEntries.Count == 0)
        {
            return new[] { "- No assets with valid offsets found in map.json." };
        }

        assetEntries.Sort((a, b) => a.offset.CompareTo(b.offset));
        var assetsBinSize = new FileInfo(assetsBinPath).Length;
        var lines = new List<string> { $"- Total assets: {assetEntries.Count}", $"- assets.bin size: {FormatBytes(assetsBinSize)}", "- Assets:" };
        for (var i = 0; i < assetEntries.Count; i++)
        {
            var current = assetEntries[i];
            var nextOffset = i + 1 < assetEntries.Count ? assetEntries[i + 1].offset : assetsBinSize;
            var size = Math.Max(0, nextOffset - current.offset);
            lines.Add($"  - {current.name} | {FormatBytes(size)} | {current.path}");
        }

        return lines;
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
}
