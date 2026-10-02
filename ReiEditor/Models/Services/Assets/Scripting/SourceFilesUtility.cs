using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        public List<SerializableObjectInfo> DataAssetDeclarations { get; } = new();
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

    public IEnumerable<string> GetSourceRoots() => new[] { _resourceService.GetScriptsPath() }
        .Concat(ProjectSourceFiles.IncludeRoots(_engineSettings.GetEngineSourceIncludes()))
        .Distinct(StringComparer.OrdinalIgnoreCase);

    public ProcessedFilesResult ProcessFiles()
    {
        _processedFiles = new();
        
        var paths = GetSourceRoots();
        
        AreSourceFilesValid = true;

        var sourceFiles = new List<(string Path, string Contents)>();
        var visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rootDir in paths)
        {
            foreach (var path in Directory.EnumerateFiles(rootDir, $"*{FileExtensions.H}", SearchOption.AllDirectories))
            {
                if (!visitedFiles.Add(Path.GetFullPath(path))) continue;

                try
                {
                    sourceFiles.Add((path, File.ReadAllText(path)));
                }
                catch (Exception e)
                {
                    _logger.LogError($"Exception while reading source file {path}. \n {e}");
                    AreSourceFilesValid = false;
                }
            }
        }

        foreach (var sourceFile in sourceFiles)
        {
            try
            {
                TryAddSerializableEnum(sourceFile.Contents, sourceFile.Path, _processedFiles.SerializableEnums);
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception while parsing enum file {sourceFile.Path}. \n {e}");
                AreSourceFilesValid = false;
            }
        }

        foreach (var sourceFile in sourceFiles)
        {
            try
            {
                TryAddSerializableObject(sourceFile.Contents, sourceFile.Path, _processedFiles.SerializableObjects, _processedFiles.DataAssetDeclarations);
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception while parsing object file {sourceFile.Path}. \n {e}");
                AreSourceFilesValid = false;
            }
        }
        
        return _processedFiles;
    }

    private void TryAddSerializableObject(string fileContents, string path, List<SerializableObjectInfo> result, List<SerializableObjectInfo> dataAssetDeclarations)
    {
        var isSerializable = TryGetSerializableObjectNameFrom(fileContents, out var name, out var isTemplate, out var isDataAsset);
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
        if (isDataAsset) dataAssetDeclarations.Add(serializableObject);
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
        return TryGetSerializableObjectNameFrom(text, out name, out isTemplate, out _);
    }

    public bool TryGetSerializableObjectNameFrom(string text, out string name, out bool isTemplate, out bool isDataAsset)
    {
        name = "";
        isTemplate = false;
        isDataAsset = false;

        Match? selectedMatch = null;
        foreach (var (macro, dataAsset) in new[]
                 {
                     (SourceFileMacrosConstants.SERIALIZABLE_BODY, false),
                     (SourceFileMacrosConstants.DATA_ASSET_BODY, true),
                 })
        {
            var regex = new Regex($@"{macro}\((?<name>.*?)\)");
            foreach (Match match in regex.Matches(text))
            {
                var matchName = match.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(matchName)) continue;
                if (matchName.Contains("CLASS_NAME") || matchName.Contains("DATA_ASSET_NAME")) continue;

                if (selectedMatch != null)
                {
                    throw new Exception("Multiple serializable root objects were found in one source file. This is not supported.");
                }

                selectedMatch = match;
                isDataAsset = dataAsset;
            }
        }

        if (selectedMatch == null) return false;

        name = selectedMatch.Groups["name"].Value;

        var indexesOfTemplates = text.AllIndexesOf("template <typename");
        indexesOfTemplates.AddRange(text.AllIndexesOf("template<typename"));

        if (indexesOfTemplates.Count != 0)
        {
            var firstTemplateIdx = indexesOfTemplates.First();

            var idxOfObjectName = text.IndexOf(" " + name, StringComparison.Ordinal);
            isTemplate = firstTemplateIdx < idxOfObjectName;
        }

        return true;
    }

    public async Task<bool> IsDataAssetFileAsync(string path)
    {
        if (!Path.GetExtension(path).Equals(FileExtensions.H, StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(path)) return false;

        var fileContents = await File.ReadAllTextAsync(path);
        return TryGetSerializableObjectNameFrom(fileContents, out _, out _, out var isDataAsset) && isDataAsset;
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
        var declarationIndex = 0;

        foreach (var annotation in SerializedPropertyAnnotationParser.Parse(text))
        {
            var serializedIndex = annotation.Index;
            var endIdx = annotation.EndIndex;
            var substring = text.Substring(serializedIndex, endIdx - serializedIndex);
            var hideInEditor = annotation.HideInEditor;
            var words = substring.Split().ToList();
            words.RemoveAll(string.IsNullOrWhiteSpace);

            if (words.Contains("="))
            {
                var equalsIdx = words.IndexOf("=");
                
                var variableType = words[equalsIdx - 2];
                var serializedType = GetSerializedTypeForVariableType(variableType);
                if (serializedType == SerializedTypeEnum.Invalid) continue;

                var variableName = words[equalsIdx - 1];
                var defaultValue = words[equalsIdx + 1];
                result.Add(variableName, CreateSerializedPropertyData(variableType, defaultValue, hideInEditor, annotation.Header, declarationIndex++));
            }
            else
            {
                var variableType = words[^2];
                var serializedType = GetSerializedTypeForVariableType(variableType);
                if (serializedType == SerializedTypeEnum.Invalid) continue;

                var variableName = words[^1];
                result.Add(variableName, CreateSerializedPropertyData(variableType, null, hideInEditor, annotation.Header, declarationIndex++));
            }
        }

        return result;
    }

    public List<string> GetRequiredComponentNames(string text)
    {
        text = RemoveComments(text);
        var result = new List<string>();
        var regex = new Regex($@"{SourceFileMacrosConstants.REQUIRE_COMPONENT}\((?<name>.*?)\)");

        foreach (Match match in regex.Matches(text))
        {
            var name = match.Groups["name"].Value.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Contains("COMPONENT_NAME")) continue;

            var normalizedName = SerializedTypeNameParser.GetBaseTypeName(name);
            if (!result.Contains(normalizedName)) result.Add(normalizedName);
        }

        return result;
    }

    private SerializableObjectInfo.SerializedPropertyData CreateSerializedPropertyData(string variableType, string? defaultValue, bool hideInEditor, string? headerBefore, int declarationIndex)
    {
        var sourceType = SerializedTypeNameParser.NormalizeSourceType(variableType);
        var templateTypeName = GetTemplateTypeName(sourceType);
        var serializedType = GetSerializedTypeForVariableType(variableType);

        var itemType = SerializedTypeEnum.Invalid;
        string? itemSourceType = null;
        string? itemTemplateTypeName = null;

        if (serializedType == SerializedTypeEnum.Collection && templateTypeName != null)
        {
            itemSourceType = templateTypeName;
            itemTemplateTypeName = GetTemplateTypeName(itemSourceType);
            itemType = GetSerializedTypeForVariableType(itemSourceType);
        }

        return new SerializableObjectInfo.SerializedPropertyData(
            serializedType,
            sourceType,
            templateTypeName,
            itemType,
            itemSourceType,
            itemTemplateTypeName,
            defaultValue,
            hideInEditor,
            headerBefore,
            declarationIndex);
    }

    private SerializedTypeEnum GetSerializedTypeForVariableType(string type)
    {
        var normalizedType = SerializedTypeNameParser.NormalizeSourceType(type);
        var typeWithoutNamespace = SerializedTypeNameParser.GetBaseTypeName(normalizedType);
        
        if (typeWithoutNamespace is "int" or "i32" or "u32") return SerializedTypeEnum.Integer;
        
        if (typeWithoutNamespace is "string") return SerializedTypeEnum.String;
        
        if (typeWithoutNamespace is "bool") return SerializedTypeEnum.Boolean;
        
        if (typeWithoutNamespace is "float" or "f32" or "double") return SerializedTypeEnum.Float;

        if (typeWithoutNamespace is "vector") return SerializedTypeEnum.Collection;

        if (_processedFiles.SerializableEnums.Exists(x => x.EnumName == typeWithoutNamespace))
        {
            return SerializedTypeEnum.Enum;
        }
        
        return SerializedTypeEnum.Custom;
    }

    public static string? GetTemplateTypeName(string type)
    {
        return SerializedTypeNameParser.GetTemplateTypeName(type);
    }

    private static string RemoveComments(string original)
    {
        const string TOKEN_PATTERN = SerializedPropertyAnnotationParser.LITERAL_PATTERN +
            @"|//[^\r\n]*|/\*[\s\S]*?(?:\*/|\z)";

        return Regex.Replace(original, TOKEN_PATTERN, match =>
        {
            if (match.Value[0] != '/') return match.Value;

            var whitespace = match.Value.ToCharArray();
            for (var index = 0; index < whitespace.Length; index++)
            {
                if (whitespace[index] is not ('\r' or '\n')) whitespace[index] = ' ';
            }

            return new string(whitespace);
        });
    }
}
