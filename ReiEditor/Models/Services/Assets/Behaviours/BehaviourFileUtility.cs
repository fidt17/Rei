using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourFileUtility
{
    private readonly IResourceService _resourceService;
    private readonly string _root;

    public BehaviourFileUtility(IResourceService resourceService)
    {
        _resourceService = resourceService;
        _root = _resourceService.GetProjectPath();
    }

    public List<ObjectFile<string>> GetAllBehaviours()
    {
        var headers = Directory.EnumerateFiles(_root, $"*{FileExtensions.H}", SearchOption.AllDirectories).ToList();
        var behaviours = new List<ObjectFile<string>>();
        headers.ForEach(x =>
        {
            var fileContents = File.ReadAllText(x);
            var isBehaviour = TryGetBehaviourNameFrom(fileContents, out _);
            if (isBehaviour)
            {
                behaviours.Add(new ObjectFile<string>(fileContents, x));
            }
        });

        return behaviours;
    }

    public async Task<List<ObjectFile<AssetMeta>>> GetAllBehaviourMetas()
    {
        var metaFiles = Directory.EnumerateFiles(_root, $"*.h{FileExtensions.META}", SearchOption.AllDirectories);
        var metas = new List<ObjectFile<AssetMeta>>();
        
        foreach (var metaFile in metaFiles)
        {
            var meta = await _resourceService.Load<AssetMeta>(metaFile);
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
    
    public List<string> GetSerializedProperties(string text)
    {
        var result = new List<string>();
        var serializedIndexes = text.AllIndexesOf(BehaviourMacrosConstants.SERIALIZED);

        foreach (var serializedIndex in serializedIndexes)
        {
            var endIdx = text.IndexOf(';', serializedIndex);
            var substring = text.Substring(serializedIndex, endIdx - serializedIndex);
            var words = substring.Split().ToList();
            words.RemoveAll(string.IsNullOrWhiteSpace);
            var variableName = words[^1];
            result.Add(variableName);
        }

        return result;
    }
}