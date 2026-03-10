using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Engine.Api;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public interface IAssetBuildCachePipeline
{
    AssetsBuildResult BuildAssets(
        IEngineApi engineApi,
        IEnumerable<AssetInfo> assetInfos,
        string cacheDirectory,
        string assetsBinPath,
        bool forceRebuild = false,
        Action<AssetBuildProgressInfo>? onAssetBuilding = null);
}
