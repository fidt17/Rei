using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetsService
{
	string AllocateAssetId();

	Task RefreshAssets();
	Task<bool> Create(Asset asset, string projectPath);
	bool Exists<T>(string assetId) where T : Asset;
	Task<T?> Load<T>(string assetId) where T : Asset;
	
	Task SaveProject();

	Task<IEnumerable<AssetPath>> GetBuildDirtyAssets();
}