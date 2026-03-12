using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public class ProjectAssetMoveCommand : IProjectAssetMoveCommand
{
    private readonly IAssetOperationsService _assetOperationsService;
    private readonly ILogger<ProjectAssetMoveCommand> _logger;

    public ProjectAssetMoveCommand(
        IAssetOperationsService assetOperationsService,
        ILogger<ProjectAssetMoveCommand> logger)
    {
        _assetOperationsService = assetOperationsService;
        _logger = logger;
    }

    public async Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset, string destinationFolder)
    {
        _logger.Log($"Execute. Path: {asset.FullPath}. Destination: {destinationFolder}. IsDirectory: {asset.IsDirectory}");
        
        await _assetOperationsService.MoveAsync(asset.FullPath, destinationFolder);
        
        return new ProjectAssetCommandResult(asset.IsDirectory, Path.Combine(destinationFolder, Path.GetFileName(asset.FullPath)));
    }
}
