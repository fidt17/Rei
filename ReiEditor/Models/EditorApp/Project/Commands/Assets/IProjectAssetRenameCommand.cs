using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public interface IProjectAssetRenameCommand
{
    Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset, string newName);
}
