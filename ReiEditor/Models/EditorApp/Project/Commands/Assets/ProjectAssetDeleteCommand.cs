using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public class ProjectAssetDeleteCommand : IProjectAssetDeleteCommand
{
    private readonly IAssetOperationsService _assetOperationsService;
    private readonly ILogger<ProjectAssetDeleteCommand> _logger;

    public ProjectAssetDeleteCommand(
        IAssetOperationsService assetOperationsService,
        ILogger<ProjectAssetDeleteCommand> logger)
    {
        _assetOperationsService = assetOperationsService;
        _logger = logger;
    }

    public async Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset)
    {
        _logger.Log($"Execute. Path: {asset.FullPath}. IsDirectory: {asset.IsDirectory}");
        
        await _assetOperationsService.DeleteAsync(asset.FullPath, asset.IsDirectory);
        
        return new ProjectAssetCommandResult(asset.IsDirectory);
    }
}
