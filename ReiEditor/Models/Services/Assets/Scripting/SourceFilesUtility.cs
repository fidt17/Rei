using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Assets.Scripting;

public class SourceFilesUtility
{
    private readonly IResourceService _resourceService;
    private readonly IEngineSettingsProvider _engineSettings;
    private readonly ILogger<SourceFilesUtility> _logger;

    public SourceFilesUtility(IResourceService resourceService, IEngineSettingsProvider engineSettings, ILogger<SourceFilesUtility> logger)
    {
        _resourceService = resourceService;
        _engineSettings = engineSettings;
        _logger = logger;
    }

    public List<SerializableObjectInfo> FindAllSerializableObjects()
    {
        var result = new List<SerializableObjectInfo>();
        
        var paths = new List<string>
        {
            _resourceService.GetScriptsPath(),
            _engineSettings.GetEnginePath(),
        };
        
        foreach (var rootDir in paths)
        {
            foreach (var path in Directory.EnumerateFiles(rootDir, $"*{FileExtensions.H}", SearchOption.AllDirectories))
            {
                var fileContents = File.ReadAllText(path);
                var isSerializable = TryGetSerializableObjectNameFrom(fileContents, out var name);
                if (!isSerializable) continue;

                var namespaceStr = GetObjectNamespaceFrom(fileContents);
                var properties = GetSerializedProperties(fileContents);
                var serializableObject = new SerializableObjectInfo(namespaceStr, name, new ObjectFile<string>(fileContents, path), properties, path);

                if (result.Exists(x => x.ObjectName == serializableObject.ObjectName))
                {
                    _logger.LogError($"Found multiple serializable objects with same name: {serializableObject.ObjectName}. This is not supported. " +
                                     $"Serializable objects name must be unique.");
                    continue;
                }
                
                result.Add(serializableObject);
            }
        }

        return result;
    }
    
    public bool TryGetSerializableObjectNameFrom(string text, out string name)
    {
        name = "";
        var regex = new Regex($".*{BehaviourMacrosConstants.SERIALIZABLE_BODY}\\((.*)\\).*");
        if (!regex.IsMatch(text)) return false;
            
        name = regex.Match(text).Groups[1].Value;
        if (name == "CLASS_NAME") return false;

        return true;
    }
    
    public static string GetObjectNamespaceFrom(string text)
    {
        const string NAMESPACE = "namespace";
        var namespaceIndexes = text.AllIndexesOf(NAMESPACE);
        if (namespaceIndexes.Count == 0) return "";
        if (namespaceIndexes.Count > 1)
        {
            throw new Exception("Multiple or nested namespaces were found in the behaviour file. This is not supported.");
        }

        var startIndex = namespaceIndexes[0] + NAMESPACE.Length;
        int endIndex = startIndex;

        for (; endIndex < text.Length; endIndex++)
        {
            var ch = text[endIndex];
            if (ch is '{' or '\r' or '\n') break;
        }

        var result = text.Substring(startIndex, endIndex - startIndex);
        result = result.Replace(" ", "");

        return result;
    }
    
    public static Dictionary<string, SerializableObjectInfo.SerializedPropertyData> GetSerializedProperties(string text)
    {
        text = RemoveComments(text);
        var result = new Dictionary<string, SerializableObjectInfo.SerializedPropertyData>();
        var serializedIndexes = text.AllIndexesOf(BehaviourMacrosConstants.SERIALIZE);

        foreach (var serializedIndex in serializedIndexes)
        {
            var endIdx = text.IndexOf(';', serializedIndex);
            var substring = text.Substring(serializedIndex, endIdx - serializedIndex);
            var words = substring.Split().ToList();
            words.RemoveAll(string.IsNullOrWhiteSpace);

            if (words.Contains("="))
            {
                var equalsIdx = words.IndexOf("=");
                
                var variableType = words[equalsIdx - 2];
                var serializedType = GetSerializedTypeForVariableType(variableType);
                if (serializedType == SerializedTypeEnum.Invalid) continue;

                var variableName = words[equalsIdx - 1];
                var variableTypeWithoutNamespace = variableType.Split("::").Last();

                var defaultValue = words[equalsIdx + 1];
                
                result.Add(variableName, new SerializableObjectInfo.SerializedPropertyData(serializedType, variableTypeWithoutNamespace, defaultValue));
            }
            else
            {
                var variableType = words[^2];
                var serializedType = GetSerializedTypeForVariableType(variableType);
                if (serializedType == SerializedTypeEnum.Invalid) continue;

                var variableName = words[^1];
                var variableTypeWithoutNamespace = variableType.Split("::").Last();
                result.Add(variableName, new SerializableObjectInfo.SerializedPropertyData(serializedType, variableTypeWithoutNamespace, null));
            }
        }

        return result;
    }

    private static SerializedTypeEnum GetSerializedTypeForVariableType(string type)
    {
        if (type is "int" or "i32" or "u32")
        {
            return SerializedTypeEnum.Integer;
        }
        
        if (type is "std::string" or "string")
        {
            return SerializedTypeEnum.String;
        }
        
        if (type is "bool")
        {
            return SerializedTypeEnum.Boolean;
        }
        
        if (type is "float" or "f32" or "double")
        {
            return SerializedTypeEnum.Float;
        }

        return SerializedTypeEnum.Custom;
    }

    private static string RemoveComments(string original)
    {
        return Regex.Replace(original, @"((\/[*])([\s\S]+)([*]\/))|([/]{2,}[^\n]+)", "");
    }
}