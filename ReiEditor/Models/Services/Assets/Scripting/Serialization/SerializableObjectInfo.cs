using System.Collections.Generic;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

public class SerializableObjectInfo
{
    public class SerializedPropertyData
    {
        public SerializedTypeEnum Type { get; }
        public string SourceType { get; }
        public string? DefaultValue { get; }

        public SerializedPropertyData(SerializedTypeEnum type, string sourceType, string? defaultValue)
        {
            Type = type;
            SourceType = sourceType;
            DefaultValue = defaultValue;
        }
    }
    
    public string Namespace { get; }
    public string ObjectName { get; }
    public ObjectFile<string> Source { get; }
    public string IncludePath { get; }
    
    public IReadOnlyDictionary<string, SerializedPropertyData> SerializedProperties => _serializedProperties;
    
    private readonly Dictionary<string, SerializedPropertyData> _serializedProperties;

    public SerializableObjectInfo(string ns, string objectName, ObjectFile<string> source, Dictionary<string, SerializedPropertyData> serializedProperties, string includePath)
    {
        Namespace = ns;
        ObjectName = objectName;
        Source = source;
        _serializedProperties = serializedProperties;
        IncludePath = includePath;
    }
}