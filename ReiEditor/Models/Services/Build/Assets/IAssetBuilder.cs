using System;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build.Assets;

public interface IAssetBuilder
{
	Task BuildAssets(
        BuildExecutionContext buildContext,
        bool forceRebuild = false,
        Action<AssetBuildProgressInfo>? onAssetBuilding = null);
}
