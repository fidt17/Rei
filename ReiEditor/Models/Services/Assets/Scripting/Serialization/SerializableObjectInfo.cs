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
        public string? TemplateTypeName { get; }
        public SerializedTypeEnum ItemType { get; }
        public string? ItemSourceType { get; }
        public string? ItemTemplateTypeName { get; }
        public string? DefaultValue { get; }
        public bool HideInEditor { get; }

        public SerializedPropertyData(
            SerializedTypeEnum type,
            string sourceType,
            string? templateTypeName,
            SerializedTypeEnum itemType,
            string? itemSourceType,
            string? itemTemplateTypeName,
            string? defaultValue,
            bool hideInEditor)
        {
            Type = type;
            SourceType = sourceType;
            TemplateTypeName = templateTypeName;
            ItemType = itemType;
            ItemSourceType = itemSourceType;
            ItemTemplateTypeName = itemTemplateTypeName;
            DefaultValue = defaultValue;
            HideInEditor = hideInEditor;
        }
    }
    
    public string Namespace { get; }
    public string ObjectName { get; }
    public ObjectFile<string> Source { get; }
    public string IncludePath { get; }
    public bool IsTemplate { get; }
    
    public IReadOnlyDictionary<string, SerializedPropertyData> SerializedProperties => _serializedProperties;
    
    private readonly Dictionary<string, SerializedPropertyData> _serializedProperties;

    public SerializableObjectInfo(string ns, string objectName, bool isTemplate, ObjectFile<string> source, Dictionary<string, SerializedPropertyData> serializedProperties, string includePath)
    {
        Namespace = ns;
        ObjectName = objectName;
        Source = source;
        IsTemplate = isTemplate;
        _serializedProperties = serializedProperties;
        IncludePath = includePath;
    }
}
