using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Build.Assets;

public interface IAssetBuilder
{
	Task Build(AssetInfo assetInfo, string buildDir);
}