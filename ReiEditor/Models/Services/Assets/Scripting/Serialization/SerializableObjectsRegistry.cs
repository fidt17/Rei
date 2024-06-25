using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

public class SerializableObjectsRegistry : ISerializableObjectsRegistry
{
    private readonly List<SerializableObjectInfo> _serializableObjects = new();
    private readonly SourceFilesUtility _sourceFilesUtility;

    public SerializableObjectsRegistry(SourceFilesUtility sourceFilesUtility)
    {
        _sourceFilesUtility = sourceFilesUtility;
    }

    public IEnumerable<SerializableObjectInfo> GetObjects() => _serializableObjects;

    public Task Refresh()
    {
        _serializableObjects.Clear();
        _serializableObjects.AddRange(_sourceFilesUtility.FindAllSerializableObjects());
        
        return Task.CompletedTask;
    }

    public SerializableObjectInfo? GetObject(string objectName)
    {
        return _serializableObjects.Find(x => x.ObjectName == objectName);
    }
}