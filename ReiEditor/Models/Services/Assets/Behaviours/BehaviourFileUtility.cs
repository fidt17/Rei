using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Behaviours.Types;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourFileUtility
{
    private readonly IResourceService _resourceService;

    public BehaviourFileUtility(IResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    public List<ObjectFile<string>> GetAllBehaviours()
    {
        var behaviours = new List<ObjectFile<string>>();
        foreach (var x in _resourceService.GetAllWithExtension(FileExtensions.H).ToList())
        {
            var fileContents = File.ReadAllText(x);
            var isBehaviour = TryGetBehaviourNameFrom(fileContents, out _);
            if (isBehaviour)
            {
                behaviours.Add(new ObjectFile<string>(fileContents, x));
            }
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
        if (type == "std::string" || type == "string")
        {
            return SerializedTypeEnum.String;
        }

        return SerializedTypeEnum.Invalid;
    }

    private string RemoveComments(string original)
    {
        return Regex.Replace(original, @"((\/[*])([\s\S]+)([*]\/))|([/]{2,}[^\n]+)", "");
    }
}