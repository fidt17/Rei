using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public class ProjectAssetRenameCommand : IProjectAssetRenameCommand
{
    private readonly IAssetOperationsService _assetOperationsService;
    private readonly ILogger<ProjectAssetRenameCommand> _logger;

    public ProjectAssetRenameCommand(
        IAssetOperationsService assetOperationsService,
        ILogger<ProjectAssetRenameCommand> logger)
    {
        _assetOperationsService = assetOperationsService;
        _logger = logger;
    }

    public async Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset, string newName)
    {
        _logger.Log($"Renaming {asset.FullPath} to {newName}");
        
        await _assetOperationsService.RenameAsync(asset.FullPath, newName);

        var parentDirectory = Path.GetDirectoryName(asset.FullPath) ?? "";
        return new ProjectAssetCommandResult(asset.IsDirectory, Path.Combine(parentDirectory, newName.Trim()));
    }
}
