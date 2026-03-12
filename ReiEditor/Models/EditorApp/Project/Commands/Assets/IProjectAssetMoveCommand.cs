using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public interface IProjectAssetMoveCommand
{
    Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset, string destinationFolder);
}
