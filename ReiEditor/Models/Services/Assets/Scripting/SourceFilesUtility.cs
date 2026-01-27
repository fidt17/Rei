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
    public class ProcessedFilesResult
    {
        public List<SerializableObjectInfo> SerializableObjects { get; } = new();
        public List<SerializableEnum> SerializableEnums { get; } = new();
    }

    public bool AreSourceFilesValid { get; private set; }

    private ProcessedFilesResult _processedFiles = new();
    
    private readonly IResourceService _resourceService;
    private readonly IEngineSettingsProvider _engineSettings;
    private readonly ILogger<SourceFilesUtility> _logger;

    public SourceFilesUtility(IResourceService resourceService, IEngineSettingsProvider engineSettings, ILogger<SourceFilesUtility> logger)
    {
        _resourceService = resourceService;
        _engineSettings = engineSettings;
        _logger = logger;
    }

    public ProcessedFilesResult ProcessFiles()
    {
        _processedFiles = new();
        
        var paths = new List<string>
        {
            _resourceService.GetScriptsPath(),
            _engineSettings.GetEnginePath(),
        };
        
        AreSourceFilesValid = true;
        
        foreach (var rootDir in paths)
        {
            foreach (var path in Directory.EnumerateFiles(rootDir, $"*{FileExtensions.H}", SearchOption.AllDirectories))
            {
                try
                {
                    var fileContents = File.ReadAllText(path);

                    TryAddSerializableObject(fileContents, path, _processedFiles.SerializableObjects);
                    TryAddSerializableEnum(fileContents, path, _processedFiles.SerializableEnums);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Exception while parsing file {path}. \n {e}");
                    AreSourceFilesValid = false;
                }
            }
        }
        
        return _processedFiles;
    }

    private void TryAddSerializableObject(string fileContents, string path, List<SerializableObjectInfo> result)
    {
        var isSerializable = TryGetSerializableObjectNameFrom(fileContents, out var name, out var isTemplate);
        if (!isSerializable) return;

        var namespaceStr = GetObjectNamespaceFrom(fileContents, path);
        var properties = GetSerializedProperties(fileContents);
        var serializableObject = new SerializableObjectInfo(namespaceStr, name, isTemplate, new ObjectFile<string>(fileContents, path), properties, path);

        if (result.Exists(x => x.ObjectName == serializableObject.ObjectName))
        {
            _logger.LogError($"Found multiple serializable objects with same name: {serializableObject.ObjectName}. This is not supported. " +
                             $"Serializable objects name must be unique.");
            return;
        }
                
        result.Add(serializableObject);
    }
    
    private void TryAddSerializableEnum(string fileContents, string path, List<SerializableEnum> result)
    {
        var hasEnum = TryGetEnumNameFrom(fileContents, out var name);
        if (!hasEnum) return;

        var namespaceStr = GetObjectNamespaceFrom(fileContents, path);
        var enumObject = new SerializableEnum
        {
            Namespace = namespaceStr,
            EnumName = name,
            IncludePath = path
        };

        if (result.Exists(x => x.EnumName == enumObject.EnumName))
        {
            _logger.LogError($"Found multiple serializable enums with same name: {enumObject.EnumName}. This is not supported. " +
                             $"Serializable enum name must be unique.");
            return;
        }
        
        string escapedEnumName = Regex.Escape(enumObject.EnumName); 
        string enumBodyPattern = $@"(?ms){SourceFileMacrosConstants.SERIALIZABLE_ENUM}\({escapedEnumName}\)\s*\{{\s*(.*?)\s*\}};?"; // Matches SERIALIZABLE_ENUM(enumName) { ... };
        Match enumMatch = Regex.Match(fileContents, enumBodyPattern, RegexOptions.Singleline);

        if (enumMatch.Success)
        {
            string enumBody = enumMatch.Groups[1].Value.Trim();
            string[] enumOptions = enumBody.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            int currentValue = 0;
            foreach (string option in enumOptions)
            {
                string trimmedOption = option.Trim();

                // Check if the option has an explicit value assignment (e.g., "SomeOther = 3")
                Match assignmentMatch = Regex.Match(trimmedOption, @"(\w+)\s*=\s*(\d+)");
                if (assignmentMatch.Success)
                {
                    string optionName = assignmentMatch.Groups[1].Value.Trim();
                    int assignedValue = int.Parse(assignmentMatch.Groups[2].Value.Trim());
                    enumObject.Options[optionName] = assignedValue;
                    currentValue = assignedValue + 1; // Increment for the next option, if it doesn't have an explicit value
                }
                else
                {
                    // No explicit value, assign the current value
                    enumObject.Options[trimmedOption] = currentValue;
                    currentValue++;
                }
            }
        }
        else
        {
            return;
        }                
        
        result.Add(enumObject);
    }

    public bool TryGetSerializableObjectNameFrom(string text, out string name, out bool isTemplate)
    {
        name = "";
        isTemplate = false;
        
        var regex = new Regex($".*{SourceFileMacrosConstants.SERIALIZABLE_BODY}\\((.*)\\).*");
        
        if (!regex.IsMatch(text)) return false;
            
        name = regex.Match(text).Groups[1].Value;
        if (name == "CLASS_NAME") return false;

        var indexesOfTemplates = text.AllIndexesOf("template <typename");
        indexesOfTemplates.AddRange(text.AllIndexesOf("template<typename"));

        // check that template is before class name and not inside over some method
        if (indexesOfTemplates.Count != 0)
        {
            var firstTemplateIdx = indexesOfTemplates.First();
            
            var idxOfObjectName = text.IndexOf(" " + name, StringComparison.Ordinal);
            isTemplate = firstTemplateIdx < idxOfObjectName;
        }

        return true;
    }
    
    public bool TryGetEnumNameFrom(string text, out string name)
    {
        name = "";

        var regex = new Regex($@"{SourceFileMacrosConstants.SERIALIZABLE_ENUM}\((?<enumName>[A-Za-z0-9_]+)\)");        
        
        if (!regex.IsMatch(text)) return false;
            
        var matches = regex.Matches(text);
        if (matches.Count == 0) return false;
        if (matches.Count > 1) throw new Exception("Multiple serializable enums were found in one source file. This is not supported.");

        name = matches[0].Groups["enumName"].Value;
        if (name == "ENUM_NAME") return false;

        return true;
    }
    
    public static string GetObjectNamespaceFrom(string text, string path)
    {
        const string NAMESPACE = "namespace";
        var namespaceIndexes = text.AllIndexesOf(NAMESPACE);
        if (namespaceIndexes.Count == 0) return "";
        if (namespaceIndexes.Count > 1)
        {
            throw new Exception($"Multiple or nested namespaces were found in the behaviour file path={path}. This is not supported.");
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
    
    public Dictionary<string, SerializableObjectInfo.SerializedPropertyData> GetSerializedProperties(string text)
    {
        text = RemoveComments(text);
        var result = new Dictionary<string, SerializableObjectInfo.SerializedPropertyData>();
        var serializedIndexes = text.AllIndexesOf(SourceFileMacrosConstants.SERIALIZE);

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

    private SerializedTypeEnum GetSerializedTypeForVariableType(string type)
    {
        var typeParts = type.Split("::");
        var typeWithoutNamespace = typeParts.Last();
        
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

        if (_processedFiles.SerializableEnums.Exists(x => x.EnumName == typeWithoutNamespace))
        {
            return SerializedTypeEnum.Enum;
        }
        
        return SerializedTypeEnum.Custom;
    }

    private static string RemoveComments(string original)
    {
        return Regex.Replace(original, @"((\/[*])([\s\S]+)([*]\/))|([/]{2,}[^\n]+)", "");
    }
}