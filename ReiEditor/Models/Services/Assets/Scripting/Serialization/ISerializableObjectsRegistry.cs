using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

public interface ISerializableObjectsRegistry
{
    IEnumerable<SerializableObjectInfo> GetObjects();
    Task Refresh();
    SerializableObjectInfo? GetObject(string objectName);
    SerializableEnum? GetEnum(string enumName);
}