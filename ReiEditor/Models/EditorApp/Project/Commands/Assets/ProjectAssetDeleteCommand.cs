using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public sealed class ProjectAssetDeleteCommand(
    IAssetOperationsService operations,
    IEditorCommandService commands,
    ILogger<ProjectAssetDeleteCommand> logger)
    : IProjectAssetDeleteCommand, IEditorCommand<IReadOnlyList<ProjectAssetCommandTarget>, ProjectAssetCommandResult?>
{
    public Task<ProjectAssetCommandResult?> ExecuteAsync(IReadOnlyList<ProjectAssetCommandTarget> assets)
    {
        if (assets.Count == 0 || commands.IsBusy) return Task.FromResult<ProjectAssetCommandResult?>(null);
        return commands.ExecuteAsync(this, assets);
    }

    async Task<ProjectAssetCommandResult?> IEditorCommand<IReadOnlyList<ProjectAssetCommandTarget>, ProjectAssetCommandResult?>.ExecuteAsync(IReadOnlyList<ProjectAssetCommandTarget> assets)
    {
        logger.Log($"Moving {assets.Count} project item(s) to the Recycle Bin.");
        var deleted = await operations.DeleteAsync(assets.Select(asset => asset.FullPath).ToArray());
        return deleted.Count == 0 ? null : new ProjectAssetCommandResult(deleted.Any(asset => asset.IsDirectory));
    }
}
