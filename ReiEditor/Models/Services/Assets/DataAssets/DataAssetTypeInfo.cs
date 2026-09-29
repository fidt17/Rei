using System.Collections.Generic;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;

namespace ReiEditor.Models.Services.Assets.DataAssets;

public sealed class DataAssetTypeInfo
{
    public int TypeId { get; }
    public SerializableObjectInfo SerializableObject { get; }

    public string Namespace => SerializableObject.Namespace;
    public string ObjectName => SerializableObject.ObjectName;
    public IReadOnlyDictionary<string, SerializableObjectInfo.SerializedPropertyData> SerializedProperties => SerializableObject.SerializedProperties;

    public DataAssetTypeInfo(int typeId, SerializableObjectInfo serializableObject)
    {
        TypeId = typeId;
        SerializableObject = serializableObject;
    }
}
