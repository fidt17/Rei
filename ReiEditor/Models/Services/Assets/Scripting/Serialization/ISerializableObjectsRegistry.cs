using System.Collections.Generic;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

public interface ISerializableObjectsRegistry
{
    IEnumerable<SerializableObjectInfo> GetObjects();
    void Replace(IEnumerable<SerializableObjectInfo> serializableObjects, IEnumerable<SerializableEnum> serializableEnums);
    SerializableObjectInfo? GetObject(string objectName);
    SerializableEnum? GetEnum(string enumName);
}