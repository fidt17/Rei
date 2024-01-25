using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourFileUtility
{
    private readonly IResourceService _resourceService;
    private readonly string _root;

    public BehaviourFileUtility(IResourceService resourceService)
    {
        _resourceService = resourceService;
        _root = _resourceService.GetFullPath();
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

    public async Task<List<ObjectFile<BehaviourMeta>>> GetAllBehaviourMetas()
    {
        var metaFiles = Directory.EnumerateFiles(_root, $"*{FileExtensions.META}", SearchOption.AllDirectories);
        var metas = new List<ObjectFile<BehaviourMeta>>();
        
        foreach (var metaFile in metaFiles)
        {
            var meta = await _resourceService.Load<BehaviourMeta>(metaFile);
            if (meta == null) continue;
            
            metas.Add(new ObjectFile<BehaviourMeta>(meta, metaFile));
        }

        return metas;
    }
    
    public bool TryGetBehaviourNameFrom(string text, out string name)
    {
        name = "";
        var regex = new Regex(".*BEHAVIOUR_BODY\\((.*)\\).*");
        if (!regex.IsMatch(text)) return false;
            
        name = regex.Match(text).Groups[1].Value;

        return true;
    }
}