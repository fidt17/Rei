using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Resources.EngineResources;

public class EngineResourcesImporter : IEngineResourcesImporter
{
    private readonly IEngineSettingsProvider _engineSettingsProvider;
    private readonly ILogger<EngineResourcesImporter> _logger;
    private readonly IResourceService _resourceService;

    public EngineResourcesImporter(ILogger<EngineResourcesImporter> logger, IEngineSettingsProvider engineSettingsProvider, IResourceService resourceService)
    {
        _logger = logger;
        _engineSettingsProvider = engineSettingsProvider;
        _resourceService = resourceService;
    }

    public Task Import()
    {
        var engineResourcesPath = _engineSettingsProvider.GetEngineResourcesDir();
        var targetDir = _resourceService.GetProjectPath("Engine Resources");

        _logger.LogWarning($"Importing engine resources");
        _logger.Log($"From {engineResourcesPath}. To {targetDir}");
        var filesCounter = 0;
        foreach (var file in _resourceService.CopyFilesRecursively(engineResourcesPath, targetDir))
        {
            _logger.Log($"{file}");
            filesCounter += 1;
        }
        
        _logger.Log($"Engine resources importing complete. Total number of files: {filesCounter}");
        
        return Task.CompletedTask;
    }
}