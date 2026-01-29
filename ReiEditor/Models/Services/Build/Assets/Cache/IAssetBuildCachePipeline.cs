using System.Collections.Generic;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public interface IAssetBuildCachePipeline
{
    AssetsBuildResult BuildAssets(IEnumerable<AssetInfo> assetInfos, string buildFolder, string assetsBinPath);
}
