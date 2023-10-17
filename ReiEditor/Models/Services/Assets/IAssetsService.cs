using System.Collections.Generic;
using System.Threading.Tasks;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetsService
{
	IObservable<bool> SaveInProcess { get; }

	string AllocateAssetId();

	Task RefreshAssets();
	Task<bool> Create(Asset asset, string projectPath);
	bool Exists<T>(string assetId) where T : Asset;

	Task<T?> Load<T>(string assetId) where T : Asset;
	Task<T?> LoadFrom<T>(string projectPath) where T : Asset;
	
	Task SaveProject();

	Task<IEnumerable<Asset>> GetBuildDirtyAssets();
}