using System.Collections.Generic;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;

namespace ReiEditor.Models.Services.Assets.Meta;

public interface IMetaFilesService
{
    Task<ObjectFile<AssetMeta>> CreateMetaFile(AssetMeta meta, string assetPath);
    
    Task RegenerateMetaFilesForTargets(IEnumerable<string> targets, IMetaFileRegenerationPolicy policy);
    Task RegenerateMetaFilesInDirectory(string directoryPath, IMetaFileRegenerationPolicy policy);
    Task RegenerateMetaFileForAsset(string assetPath, IMetaFileRegenerationPolicy policy);
    
    void MoveMetaFile(string oldAssetPath, string newAssetPath);
    
    void DeleteMetaFile(string assetPath);
    Task DeleteInvalidMetaFiles();
}
