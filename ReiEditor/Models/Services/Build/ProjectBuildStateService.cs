using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build;

public class ProjectBuildStateService : IProjectBuildStateService
{
    private sealed class ProjectBuildState
    {
        public string FormatVersion { get; set; } = "2";
        public string Status { get; set; } = BuildStateStatus.READY;
        public BuildConfigurationEnum Configuration { get; set; }
        public string EngineVersion { get; set; } = "";
        public string ClientDllPath { get; set; } = "";
        public List<TrackedFileState> SourceFiles { get; set; } = new();
        public List<TrackedFileState> AssetFiles { get; set; } = new();
        public List<TrackedFileState> EngineInputFiles { get; set; } = new();
        public List<TrackedFileState> EngineOutputFiles { get; set; } = new();
        public List<string> OutputFiles { get; set; } = new();
    }

    private sealed class TrackedFileState
    {
        public string RelativePath { get; set; } = "";
        public long Size { get; set; }
        public string ContentHash { get; set; } = "";
    }

    private static class BuildStateStatus
    {
        public const string READY = "Ready";
        public const string IN_PROGRESS = "InProgress";
        public const string FAILED = "Failed";
    }

    private static readonly string[] ENGINE_ARTIFACT_FILE_NAMES =
    {
        "Rei.dll",
        "Rei.lib",
        "assimp-vc143-mt.dll"
    };

    private static readonly string[] SOURCE_PATTERNS =
    {
        "*.cpp",
        "*.h",
        "*.sln",
        "*.vcxproj"
    };

    private const string STATE_DIRECTORY_NAME = ".rei_build_state";

    private readonly IResourceService _resourceService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IEngineSettingsProvider _engineSettingsProvider;
    private readonly IEditorBuildOutputService _editorBuildOutputService;
    private readonly ILogger<ProjectBuildStateService> _logger;

    public ProjectBuildStateService(
        IResourceService resourceService,
        IAssetRegistry assetRegistry,
        IEngineSettingsProvider engineSettingsProvider,
        IEditorBuildOutputService editorBuildOutputService,
        ILogger<ProjectBuildStateService> logger)
    {
        _resourceService = resourceService;
        _assetRegistry = assetRegistry;
        _engineSettingsProvider = engineSettingsProvider;
        _editorBuildOutputService = editorBuildOutputService;
        _logger = logger;
    }

    public async Task<Build.ProjectBuildState> CalculateState(
        BuildConfigurationEnum configuration,
        BuildExecutionContext buildContext,
        bool buildSolution,
        bool buildAssets)
    {
        if (!IsLiveOutputContext(buildContext))
        {
            return new Build.ProjectBuildState(buildSolution, buildAssets, "Build state persistence is disabled for non-live outputs.");
        }

        var statePath = GetStateFilePath(buildContext, configuration);
        var state = await TryLoadState(statePath);
        if (state == null)
        {
            return new Build.ProjectBuildState(buildSolution, buildAssets, "No persisted build state found.");
        }

        if (!string.Equals(state.Status, BuildStateStatus.READY, StringComparison.Ordinal))
        {
            return new Build.ProjectBuildState(buildSolution, buildAssets, $"Persisted build state is '{state.Status}'.");
        }

        if (!string.Equals(state.FormatVersion, "2", StringComparison.Ordinal))
        {
            return new Build.ProjectBuildState(buildSolution, buildAssets, $"Persisted build state format '{state.FormatVersion}' is outdated.");
        }

        var engineVersion = _engineSettingsProvider.GetEngineVersion();
        if (!string.Equals(state.EngineVersion, engineVersion, StringComparison.Ordinal))
        {
            return new Build.ProjectBuildState(buildSolution, buildAssets, "Engine version changed.");
        }

        var expectedClientDllPath = ResolveClientDllPath(buildContext);
        if (!string.Equals(Path.GetFullPath(state.ClientDllPath), expectedClientDllPath, StringComparison.OrdinalIgnoreCase))
        {
            return new Build.ProjectBuildState(buildSolution, buildAssets, "Client dll output path changed.");
        }

        if (buildSolution && !File.Exists(expectedClientDllPath))
        {
            return new Build.ProjectBuildState(true, buildAssets, "Client dll is missing.");
        }

        if (buildSolution)
        {
            var engineInputFiles = await GetTrackedEngineInputFiles(configuration);
            if (!TrackedFileListsMatch(engineInputFiles, state.EngineInputFiles))
            {
                _logger.Log("Engine input artifacts changed. Forcing solution rebuild.");
                return new Build.ProjectBuildState(true, buildAssets, "Engine input artifacts changed.");
            }

            var engineOutputFiles = await GetTrackedEngineOutputFiles(buildContext);
            if (!TrackedFileListsMatch(engineOutputFiles, state.EngineOutputFiles))
            {
                _logger.Log("Live engine artifacts changed or are stale. Forcing solution rebuild.");
                return new Build.ProjectBuildState(true, buildAssets, "Live engine artifacts changed or are stale.");
            }
        }

        if (buildAssets)
        {
            foreach (var outputFile in GetAssetOutputFiles(buildContext))
            {
                if (File.Exists(outputFile)) continue;
                return new Build.ProjectBuildState(buildSolution, true, $"Asset output is missing: {outputFile}");
            }
        }

        if (buildSolution)
        {
            var sourceFiles = await GetTrackedSourceFiles();
            if (!TrackedFileListsMatch(sourceFiles, state.SourceFiles))
            {
                return new Build.ProjectBuildState(true, buildAssets, "Tracked source files changed.");
            }
        }

        if (buildAssets)
        {
            var assetFiles = await GetTrackedAssetFiles();
            if (!TrackedFileListsMatch(assetFiles, state.AssetFiles))
            {
                return new Build.ProjectBuildState(buildSolution && !File.Exists(expectedClientDllPath), true, "Tracked asset files changed.");
            }
        }

        return new Build.ProjectBuildState(false, false, "Persisted build outputs are up to date.");
    }

    public void MarkBuildStarted(BuildConfigurationEnum configuration, BuildExecutionContext buildContext)
    {
        if (!IsLiveOutputContext(buildContext)) return;

        var state = new ProjectBuildState
        {
            Configuration = configuration,
            Status = BuildStateStatus.IN_PROGRESS,
            EngineVersion = _engineSettingsProvider.GetEngineVersion(),
            ClientDllPath = ResolveClientDllPath(buildContext)
        };

        SaveState(GetStateFilePath(buildContext, configuration), state);
    }

    public void MarkBuildFailed(BuildConfigurationEnum configuration, BuildExecutionContext buildContext)
    {
        if (!IsLiveOutputContext(buildContext)) return;

        var statePath = GetStateFilePath(buildContext, configuration);
        var state = TryLoadStateSync(statePath) ?? new ProjectBuildState
        {
            Configuration = configuration,
            EngineVersion = _engineSettingsProvider.GetEngineVersion(),
            ClientDllPath = ResolveClientDllPath(buildContext)
        };

        state.Status = BuildStateStatus.FAILED;
        SaveState(statePath, state);
    }

    public async Task SaveSuccessfulBuild(
        BuildConfigurationEnum configuration,
        BuildExecutionContext buildContext,
        bool buildSolution,
        bool buildAssets)
    {
        if (!IsLiveOutputContext(buildContext)) return;

        var state = new ProjectBuildState
        {
            Configuration = configuration,
            Status = BuildStateStatus.READY,
            EngineVersion = _engineSettingsProvider.GetEngineVersion(),
            ClientDllPath = ResolveClientDllPath(buildContext),
            OutputFiles = GetExpectedOutputFiles(buildContext, buildSolution, buildAssets)
        };

        if (buildSolution)
        {
            state.SourceFiles = await GetTrackedSourceFiles();
            state.EngineInputFiles = await GetTrackedEngineInputFiles(configuration);
            state.EngineOutputFiles = await GetTrackedEngineOutputFiles(buildContext);
        }
        else
        {
            var existingState = TryLoadStateSync(GetStateFilePath(buildContext, configuration));
            state.SourceFiles = existingState?.SourceFiles ?? new List<TrackedFileState>();
            state.EngineInputFiles = existingState?.EngineInputFiles ?? new List<TrackedFileState>();
            state.EngineOutputFiles = existingState?.EngineOutputFiles ?? new List<TrackedFileState>();
        }

        if (buildAssets)
        {
            state.AssetFiles = await GetTrackedAssetFiles();
        }
        else
        {
            state.AssetFiles = TryLoadStateSync(GetStateFilePath(buildContext, configuration))?.AssetFiles ?? new List<TrackedFileState>();
        }

        SaveState(GetStateFilePath(buildContext, configuration), state);
    }

    private bool IsLiveOutputContext(BuildExecutionContext buildContext)
    {
        var liveOutput = _editorBuildOutputService.GetLiveOutput();
        var liveBuildFolder = Path.GetFullPath(liveOutput.BinDirectoryPath);
        var contextBuildFolder = Path.GetFullPath(buildContext.BuildFolder);
        if (!string.Equals(liveBuildFolder, contextBuildFolder, StringComparison.OrdinalIgnoreCase)) return false;

        var contextClientDllPath = ResolveClientDllPath(buildContext);
        var liveClientDllPath = Path.GetFullPath(liveOutput.ClientDllPath);
        return string.Equals(contextClientDllPath, liveClientDllPath, StringComparison.OrdinalIgnoreCase);
    }

    private string GetStateFilePath(BuildExecutionContext buildContext, BuildConfigurationEnum configuration)
    {
        return Path.Combine(buildContext.BuildFolder, STATE_DIRECTORY_NAME, $"{configuration}.json");
    }

    private string ResolveClientDllPath(BuildExecutionContext buildContext)
    {
        if (!string.IsNullOrWhiteSpace(buildContext.ClientDllPath))
        {
            return Path.GetFullPath(buildContext.ClientDllPath);
        }

        return Path.GetFullPath(_editorBuildOutputService.GetLiveOutput().ClientDllPath);
    }

    private static bool AssetOutputsPresent(BuildExecutionContext buildContext)
    {
        return GetAssetOutputFiles(buildContext).All(File.Exists);
    }

    private List<string> GetExpectedOutputFiles(BuildExecutionContext buildContext, bool buildSolution, bool buildAssets)
    {
        var outputFiles = new List<string>();
        if (buildSolution) outputFiles.Add(ResolveClientDllPath(buildContext));

        if (buildAssets)
        {
            outputFiles.AddRange(GetAssetOutputFiles(buildContext));
        }

        return outputFiles;
    }

    private static List<string> GetAssetOutputFiles(BuildExecutionContext buildContext)
    {
        return new List<string>
        {
            Path.Combine(buildContext.ResourcesDirectoryPath, "assets.bin"),
            Path.Combine(buildContext.ResourcesDirectoryPath, "map.bin")
        };
    }

    private async Task<List<TrackedFileState>> GetTrackedSourceFiles()
    {
        var rootPath = _resourceService.GetRootPath();
        var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in SOURCE_PATTERNS)
        {
            foreach (var filePath in Directory.EnumerateFiles(rootPath, pattern, SearchOption.AllDirectories))
            {
                filePaths.Add(filePath);
            }
        }

        var trackedFiles = new List<TrackedFileState>(filePaths.Count);
        foreach (var filePath in filePaths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (filePath.Contains($"{Path.DirectorySeparatorChar}{ResourceConstants.BIN_DIR_NAME}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)) continue;
            if (filePath.Contains($"{Path.DirectorySeparatorChar}.rei_tmp{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)) continue;

            trackedFiles.Add(await CreateTrackedFileState(rootPath, filePath));
        }

        return trackedFiles;
    }

    private async Task<List<TrackedFileState>> GetTrackedAssetFiles()
    {
        var rootPath = _resourceService.GetRootPath();
        var assets = _assetRegistry.GetAllAssets()
            .Where(asset => AssetBuildPathUtility.ShouldBuildPath(asset.FullPath))
            .OrderBy(asset => asset.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var trackedFiles = new List<TrackedFileState>(assets.Count);
        foreach (var asset in assets)
        {
            trackedFiles.Add(await CreateTrackedFileState(rootPath, asset.FullPath));
        }

        return trackedFiles;
    }

    private async Task<List<TrackedFileState>> GetTrackedEngineInputFiles(BuildConfigurationEnum configuration)
    {
        var engineIncludeDir = GetEngineIncludeDir(configuration);
        var trackedFiles = new List<TrackedFileState>(ENGINE_ARTIFACT_FILE_NAMES.Length);
        foreach (var fileName in ENGINE_ARTIFACT_FILE_NAMES)
        {
            var filePath = Path.Combine(engineIncludeDir, fileName);
            if (!File.Exists(filePath))
            {
                _logger.Log($"Tracked engine input artifact is missing: {filePath}");
                return new List<TrackedFileState>();
            }

            trackedFiles.Add(await CreateTrackedFileState(engineIncludeDir, filePath));
        }

        return trackedFiles;
    }

    private async Task<List<TrackedFileState>> GetTrackedEngineOutputFiles(BuildExecutionContext buildContext)
    {
        var clientDllPath = ResolveClientDllPath(buildContext);
        var clientOutputDir = Path.GetDirectoryName(clientDllPath);
        if (string.IsNullOrWhiteSpace(clientOutputDir))
        {
            _logger.Log($"Could not resolve client output directory for '{clientDllPath}'.");
            return new List<TrackedFileState>();
        }

        var trackedFiles = new List<TrackedFileState>(2);
        foreach (var fileName in new[] { "Rei.dll", "assimp-vc143-mt.dll" })
        {
            var filePath = Path.Combine(clientOutputDir, fileName);
            if (!File.Exists(filePath))
            {
                _logger.Log($"Tracked engine output artifact is missing: {filePath}");
                return new List<TrackedFileState>();
            }

            trackedFiles.Add(await CreateTrackedFileState(clientOutputDir, filePath));
        }

        return trackedFiles;
    }

    private string GetEngineIncludeDir(BuildConfigurationEnum configuration)
    {
        return configuration == BuildConfigurationEnum.Release
            ? _engineSettingsProvider.GetEngineReleaseIncludeDir()
            : _engineSettingsProvider.GetEngineDebugIncludeDir();
    }

    private static bool TrackedFileListsMatch(IReadOnlyList<TrackedFileState> currentFiles, IReadOnlyList<TrackedFileState> persistedFiles)
    {
        if (currentFiles.Count != persistedFiles.Count) return false;

        for (var i = 0; i < currentFiles.Count; i++)
        {
            var current = currentFiles[i];
            var persisted = persistedFiles[i];
            if (!string.Equals(current.RelativePath, persisted.RelativePath, StringComparison.OrdinalIgnoreCase)) return false;
            if (current.Size != persisted.Size) return false;
            if (!string.Equals(current.ContentHash, persisted.ContentHash, StringComparison.Ordinal)) return false;
        }

        return true;
    }

    private static async Task<TrackedFileState> CreateTrackedFileState(string rootPath, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var fileInfo = new FileInfo(fullPath);
        return new TrackedFileState
        {
            RelativePath = Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/'),
            Size = fileInfo.Length,
            ContentHash = await ComputeSha256(fullPath)
        };
    }

    private static async Task<string> ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    private async Task<ProjectBuildState?> TryLoadState(string statePath)
    {
        try
        {
            if (!File.Exists(statePath)) return null;

            var json = await File.ReadAllTextAsync(statePath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<ProjectBuildState>(json);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return null;
        }
    }

    private ProjectBuildState? TryLoadStateSync(string statePath)
    {
        try
        {
            if (!File.Exists(statePath)) return null;

            var json = File.ReadAllText(statePath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<ProjectBuildState>(json);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return null;
        }
    }

    private void SaveState(string statePath, ProjectBuildState state)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(statePath);
            if (string.IsNullOrWhiteSpace(directoryPath)) return;

            Directory.CreateDirectory(directoryPath);
            var tempPath = statePath + ".tmp";
            var json = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, statePath, overwrite: true);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }
}
