using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Import;

public interface IAssetImporter
{
    event Action ImportedAssetsEvent;

    Utils.Common.IObservable<bool> IsImporting { get; }
    
    Task<List<AssetInfo>> EnsureImported() => ReimportAll();
    Task<List<AssetInfo>> ReimportAll();
    Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths);
}
