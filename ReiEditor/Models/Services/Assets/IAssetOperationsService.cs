using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetOperationsService
{
    Task RenameAsync(string assetPath, string newName);
    Task DeleteAsync(string assetPath, bool isDirectory);
    Task DuplicateAsync(string assetPath, bool isDirectory);
    Task MoveAsync(string assetPath, string destinationFolder);
    Task ImportExternalAssets(IEnumerable<string> sourcePaths, string targetFolder);
    Task CreateFolderAsync(string parentDirectory, string folderName);
}
