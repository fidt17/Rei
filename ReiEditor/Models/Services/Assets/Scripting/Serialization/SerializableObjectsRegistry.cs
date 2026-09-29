using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

public class SerializableObjectsRegistry : ISerializableObjectsRegistry
{
    private readonly object _registryLock = new();
    private List<SerializableObjectInfo> _serializableObjects = new();
    private List<SerializableEnum> _serializableEnums = new();
    private readonly ILogger<SerializableObjectsRegistry> _logger;

    public SerializableObjectsRegistry(ILogger<SerializableObjectsRegistry> logger)
    {
        _logger = logger;
    }

    public IEnumerable<SerializableObjectInfo> GetObjects()
    {
        lock (_registryLock)
        {
            return _serializableObjects.ToList();
        }
    }

    public void Replace(IEnumerable<SerializableObjectInfo> serializableObjects, IEnumerable<SerializableEnum> serializableEnums)
    {
        var objects = serializableObjects.ToList();
        var enums = serializableEnums.ToList();
        lock (_registryLock)
        {
            _serializableObjects = objects;
            _serializableEnums = enums;
        }

        LogSerializableObjects(objects);
    }

    public SerializableObjectInfo? GetObject(string objectName)
    {
        var t = objectName.AllIndexesOf("<");
        if (t.Count != 0)
        {
            objectName = objectName.Remove(t[0], objectName.Length - t[0]);
        }

        lock (_registryLock)
        {
            return _serializableObjects.Find(x => x.ObjectName == objectName);
        }
    }

    public SerializableEnum? GetEnum(string enumName)
    {
        lock (_registryLock)
        {
            return _serializableEnums.Find(x => x.EnumName == enumName);
        }
    }

    private void LogSerializableObjects(IEnumerable<SerializableObjectInfo> serializableObjects)
    {
        var log = new StringBuilder();
        log.AppendLine("Serializable objects: ");
        foreach (var objectInfo in serializableObjects)
        {
            log.AppendLine($"- {objectInfo.ObjectName}{(objectInfo.IsTemplate ? "<T>" : "")} {objectInfo.IncludePath}");
        }
        _logger.Log(log.ToString());
    }
}