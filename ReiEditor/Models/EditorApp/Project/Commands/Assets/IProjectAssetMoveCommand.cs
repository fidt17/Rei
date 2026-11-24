using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public interface IProjectAssetMoveCommand
{
    Task<IReadOnlyList<ProjectAssetCommandResult>> ExecuteAsync(IReadOnlyList<ProjectAssetCommandTarget> assets, string destinationFolder);
}
