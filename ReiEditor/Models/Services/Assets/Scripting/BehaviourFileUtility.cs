using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets.Scripting;

public class BehaviourFileUtility : IBehaviourFileUtility
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
            if (!meta.TryGetData(BehaviourMeta.Key, out BehaviourMeta? _)) continue;
            
            metas.Add(new ObjectFile<AssetMeta>(meta, metaFile));
        }

        return metas;
    }
    
    public bool TryGetBehaviourNameFrom(string text, out string name)
    {
        name = "";
        var regex = new Regex($".*{SourceFileMacrosConstants.BEHAVIOUR_BODY}\\((.*)\\).*");
        if (!regex.IsMatch(text)) return false;
            
        name = regex.Match(text).Groups[1].Value;

        return true;
    }

    public bool IsBehaviourFile(string path)
    {
        if (!File.Exists(path)) return false;

        var fileContents = File.ReadAllText(path);
        return TryGetBehaviourNameFrom(fileContents, out _);
    }
}
