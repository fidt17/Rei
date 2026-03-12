using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public interface IProjectAssetDuplicateCommand
{
    Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset);
}
