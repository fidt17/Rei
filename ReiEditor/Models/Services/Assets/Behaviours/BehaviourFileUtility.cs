using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Behaviours.Types;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourFileUtility
{
    public class BehaviourPathData
    {
        public string Content { get; }
        public string Path { get; }
        public string MetaPath { get; }
        public bool IsEngineBehaviour { get; }

        public BehaviourPathData(string content, string path, string metaPath, bool isEngineBehaviour)
        {
            Content = content;
            Path = path;
            MetaPath = metaPath;
            IsEngineBehaviour = isEngineBehaviour;
        }
    }
    
    private readonly IResourceService _resourceService;
    private readonly IEngineSettingsProvider _engineSettingsProvider;

    public BehaviourFileUtility(IResourceService resourceService, IEngineSettingsProvider engineSettingsProvider)
    {
        _resourceService = resourceService;
        _engineSettingsProvider = engineSettingsProvider;
    }

    public List<BehaviourPathData> GetAllBehaviours()
    {
        var behaviours = new List<BehaviourPathData>();

        void tryAddBehaviour(string path, bool isEngineBehaviour)
        {
            var fileContents = File.ReadAllText(path);
            var isBehaviour = TryGetBehaviourNameFrom(fileContents, out _);
            if (isBehaviour)
            {
                var metaPath = path + FileExtensions.META;

                if (isEngineBehaviour)
                {
                    metaPath = metaPath.Replace(_engineSettingsProvider.GetEngineResourcesDir(), _resourceService.GetRootPath("Internal"));
                }
                
                behaviours.Add(new BehaviourPathData(fileContents, path, metaPath, isEngineBehaviour));
            }
        }

        // project header files
        foreach (var x in _resourceService.GetAllWithExtension(FileExtensions.H).ToList())
        {
            tryAddBehaviour(x, false);
        }

        // engine behaviour header files
        var engineBehavioursPath = _engineSettingsProvider.GetEngineBehavioursDir();
        foreach (var x in Directory.EnumerateFiles(engineBehavioursPath, $"*{FileExtensions.H}", SearchOption.AllDirectories))
        {
            tryAddBehaviour(x, true);
        }

        return behaviours;
    }

    public async Task<List<ObjectFile<AssetMeta>>> GetAllBehaviourMetas()
    {
        var metas = new List<ObjectFile<AssetMeta>>();
        
        foreach (var metaFile in _resourceService.GetAllWithExtension(FileExtensions.META))
        {
            var meta = await _resourceService.TryLoad<AssetMeta>(metaFile);
            if (meta == null) continue;
            if (!meta.TryGetData(BehaviourMeta.Key, out BehaviourMeta _)) continue;
            
            metas.Add(new ObjectFile<AssetMeta>(meta, metaFile));
        }

        return metas;
    }
    
    public string GetBehaviourNamespaceFrom(string text)
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
    
    public bool TryGetBehaviourNameFrom(string text, out string name)
    {
        name = "";
        var regex = new Regex($".*{BehaviourMacrosConstants.BEHAVIOUR_BODY}\\((.*)\\).*");
        if (!regex.IsMatch(text)) return false;
            
        name = regex.Match(text).Groups[1].Value;

        return true;
    }
    
    public Dictionary<string, SerializedTypeEnum> GetSerializedProperties(string text)
    {
        text = RemoveComments(text);
        var result = new Dictionary<string, SerializedTypeEnum>();
        var serializedIndexes = text.AllIndexesOf(BehaviourMacrosConstants.SERIALIZED);

        foreach (var serializedIndex in serializedIndexes)
        {
            var endIdx = text.IndexOf(';', serializedIndex);
            var substring = text.Substring(serializedIndex, endIdx - serializedIndex);
            var words = substring.Split().ToList();
            words.RemoveAll(string.IsNullOrWhiteSpace);
            
            var variableType = words[^2];
            var serializedType = GetSerializedTypeForVariableType(variableType);
            if (serializedType == SerializedTypeEnum.Invalid) continue;

            var variableName = words[^1];
            result.Add(variableName, serializedType);
        }

        return result;
    }

    private SerializedTypeEnum GetSerializedTypeForVariableType(string type)
    {
        if (type is "int" or "i32" or "u32")
        {
            return SerializedTypeEnum.Integer;
        }
        else if (type is "std::string" or "string")
        {
            return SerializedTypeEnum.String;
        }
        else if (type is "bool")
        {
            return SerializedTypeEnum.Boolean;
        }

        return SerializedTypeEnum.Invalid;
    }

    private string RemoveComments(string original)
    {
        return Regex.Replace(original, @"((\/[*])([\s\S]+)([*]\/))|([/]{2,}[^\n]+)", "");
    }
}