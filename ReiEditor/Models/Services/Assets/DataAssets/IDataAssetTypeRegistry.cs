using System.Collections.Generic;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;

namespace ReiEditor.Models.Services.Assets.DataAssets;

public interface IDataAssetTypeRegistry
{
    IEnumerable<DataAssetTypeInfo> GetDataAssetTypes();
    DataAssetTypeInfo? GetDataAssetType(int dataAssetTypeId);
    DataAssetTypeInfo? GetDataAssetType(string objectName);
    int AllocateDataAssetTypeId();
    Task RefreshAsync(IEnumerable<SerializableObjectInfo> dataAssetDeclarations);
}
