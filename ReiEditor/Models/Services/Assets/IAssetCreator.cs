using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetCreator
{
    string AllocateAssetId();
    Task<bool> Create(Asset asset, string projectPath);
    Task<bool> Create(Asset asset, string id, string projectPath);
}
