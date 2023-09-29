using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetsService
{
	string AllocateAssetId();
	Task<bool> Create(Asset asset, string projectPath);
	Task SaveProject();
}