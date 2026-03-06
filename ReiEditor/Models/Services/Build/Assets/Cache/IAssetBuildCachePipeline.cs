using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build.Assets;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public interface IAssetBuildCachePipeline
{
    AssetsBuildResult BuildAssets(
        IEnumerable<AssetInfo> assetInfos,
        string buildFolder,
        string assetsBinPath,
        bool forceRebuild = false,
        Action<AssetBuildProgressInfo>? onAssetBuilding = null);
}
