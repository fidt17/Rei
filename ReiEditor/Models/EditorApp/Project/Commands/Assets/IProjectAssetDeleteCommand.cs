using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public interface IProjectAssetDeleteCommand
{
    Task<ProjectAssetCommandResult?> ExecuteAsync(IReadOnlyList<ProjectAssetCommandTarget> assets);
}
