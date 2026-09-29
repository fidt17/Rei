using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets.DataAssets;

public static class DataAssetFileUtility
{
    public static async Task<int?> ReadTypeIdAsync(string fullPath)
    {
        if (!Path.GetExtension(fullPath).Equals(FileExtensions.ASSET, System.StringComparison.OrdinalIgnoreCase)) return null;
        if (!File.Exists(fullPath)) return null;

        try
        {
            var root = JObject.Parse(await File.ReadAllTextAsync(fullPath));
            var typeId = root[nameof(DataAsset.DataAssetTypeId)]?.ToObject<int>();
            return typeId is >= 0 ? typeId : null;
        }
        catch
        {
            return null;
        }
    }
}
