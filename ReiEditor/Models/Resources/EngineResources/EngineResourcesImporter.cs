using System.Collections.Generic;
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

    public EngineResourcesImporter(
        ILogger<EngineResourcesImporter> logger,
        IEngineSettingsProvider engineSettingsProvider,
        IResourceService resourceService)
    {
        _logger = logger;
        _engineSettingsProvider = engineSettingsProvider;
        _resourceService = resourceService;
    }

    public Task Import()
    {
        var targetDir = _resourceService.GetProjectPath("Engine Resources");
        CopyResources(targetDir);

        _logger.Log($"Engine resources importing complete");

        return Task.CompletedTask;
    }

    private void CopyResources(string to)
    {
        var from = _engineSettingsProvider.GetEngineResourcesDir();
        
        _logger.LogWarning($"Importing engine resources");
        _logger.Log($"From {from}. To {to}");
        
        var fromPaths = new List<string>
        {
            "/shaders",
            "/textures"
        };
        
        foreach (var dirName in fromPaths)
        {
            _resourceService.CopyFilesRecursively(from + dirName, to + dirName);
        }
    }
}