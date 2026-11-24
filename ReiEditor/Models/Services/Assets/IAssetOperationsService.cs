using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetOperationsService
{
    Task<string?> RenameAsync(string assetPath, string newName);
    Task<IReadOnlyList<(string Path, bool IsDirectory)>> DeleteAsync(IReadOnlyList<string> assetPaths);
    Task DuplicateAsync(string assetPath, bool isDirectory);
    Task<string?> MoveAsync(string assetPath, string destinationFolder);
    bool CanMoveToPath(string assetPath, string targetPath);
    Task<bool> MoveToPathAsync(string assetPath, string targetPath);
    Task ImportExternalAssets(IEnumerable<string> sourcePaths, string targetFolder);
    Task CreateFolderAsync(string parentDirectory, string folderName);
}
