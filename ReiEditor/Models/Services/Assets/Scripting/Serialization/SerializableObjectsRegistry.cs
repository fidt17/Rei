using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

public class SerializableObjectsRegistry : ISerializableObjectsRegistry
{
    private readonly List<SerializableObjectInfo> _serializableObjects = new();
    private readonly List<SerializableEnum> _serializableEnums = new();
    private readonly SourceFilesUtility _sourceFilesUtility;
    private readonly ILogger<SerializableObjectsRegistry> _logger;

    public SerializableObjectsRegistry(SourceFilesUtility sourceFilesUtility, ILogger<SerializableObjectsRegistry> logger)
    {
        _sourceFilesUtility = sourceFilesUtility;
        _logger = logger;
    }

    public IEnumerable<SerializableObjectInfo> GetObjects() => _serializableObjects;

    public Task Refresh()
    {
        _serializableObjects.Clear();

        var processedFiles = _sourceFilesUtility.ProcessFiles();
        _serializableObjects.AddRange(processedFiles.SerializableObjects);
        _serializableEnums.AddRange(processedFiles.SerializableEnums);

        LogSerializableObjects();
        
        return Task.CompletedTask;
    }

    public SerializableObjectInfo? GetObject(string objectName)
    {
        var t = objectName.AllIndexesOf("<");
        if (t.Count != 0)
        {
            objectName = objectName.Remove(t[0], objectName.Length - t[0]);
        }
        
        return _serializableObjects.Find(x => x.ObjectName == objectName);
    }

    public SerializableEnum? GetEnum(string enumName)
    {
        return _serializableEnums.Find(x => x.EnumName == enumName);
    }

    private void LogSerializableObjects()
    {
        var log = new StringBuilder();
        log.AppendLine("Serializable objects: ");
        _serializableObjects.ForEach(x => log.AppendLine($"- {x.ObjectName}{(x.IsTemplate ? "<T>" : "")} {x.IncludePath}"));
        _logger.Log(log.ToString());
    }
}