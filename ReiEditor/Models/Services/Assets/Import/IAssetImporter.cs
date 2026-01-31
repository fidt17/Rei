using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Import;

public interface IAssetImporter
{
    event Action ImportedAssetsEvent;
    
    Task<List<AssetInfo>> ReimportAll();
    Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths);
}
