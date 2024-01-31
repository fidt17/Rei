using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetImporter
{
    Task<List<AssetInfo>> ReimportAll();
}