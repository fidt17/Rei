using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourRegistry : IBehaviourRegistry
{
    public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours => _behaviours;
    
    private int _maxBehaviourId = -1;
    
    private readonly Dictionary<int, BehaviourAssetInfo> _behaviours = new();
    private readonly BehaviourFileUtility _utility;
    private readonly IAssetCreator _assetCreator;
    private readonly BehaviourRegistrySourceGenerator _behaviourRegistrySourceGenerator;
    private readonly ILogger<BehaviourRegistry> _logger;

    public BehaviourRegistry(IAssetCreator assetCreator, IResourceService resourceService, ILogger<BehaviourRegistry> logger)
    {
        _assetCreator = assetCreator;
        _logger = logger;
        _utility = new BehaviourFileUtility(resourceService);
        _behaviourRegistrySourceGenerator = new BehaviourRegistrySourceGenerator(resourceService);
    }

    public bool TryGetById(int id, [NotNullWhen(returnValue: true)] out BehaviourAssetInfo? behaviour) => _behaviours.TryGetValue(id, out behaviour);

    public async Task RefreshBehaviours()
    {
        _logger.Log("Refreshing behaviours...");
        
        _behaviours.Clear();
        
        var behaviourFiles = _utility.GetAllBehaviours();
        var metaFiles = await _utility.GetAllBehaviourMetas();
        
        await RegisterBehaviours(behaviourFiles, metaFiles);
        await _behaviourRegistrySourceGenerator.GenerateBehaviourRegistrySourceFile(_behaviours);
        
        //LogBehaviours();
        _logger.Log($"Total behaviours found: {_behaviours.Count}");
    }

    private async Task RegisterBehaviours(List<ObjectFile<string>> behaviourFiles, List<ObjectFile<AssetMeta>> metaFiles)
    {
        var newBehaviours = new List<ObjectFile<string>>();
        
        foreach (var behaviourFile in behaviourFiles)
        {
            if (!_utility.TryGetBehaviourNameFrom(behaviourFile.Object, out var name)) throw new Exception($"Invalid behaviour file. {behaviourFile.FullPath}");

            var metaFile = metaFiles.Find(x => x.FullPath == behaviourFile.FullPath + FileExtensions.META);

            if (metaFile == null || !metaFile.Object.TryGetData(BehaviourMeta.Key, out BehaviourMeta? behaviourMeta))
            {
                newBehaviours.Add(behaviourFile);
                continue;
            }

            if (behaviourMeta == null) throw new Exception($"Could not find behaviour meta. {behaviourFile.FullPath}");

            var properties = _utility.GetSerializedProperties(behaviourFile.Object);
            RegisterBehaviour(new BehaviourAssetInfo(name, behaviourMeta.BehaviourId, behaviourFile, properties));
        }
        
        foreach (var behaviourFile in newBehaviours)
        {
            if (!_utility.TryGetBehaviourNameFrom(behaviourFile.Object, out var name)) throw new Exception($"Invalid behaviour file. {behaviourFile.FullPath}");
            
            var behaviourMeta = new BehaviourMeta(_maxBehaviourId + 1);
            await CreateMetaFile(behaviourFile, behaviourMeta);
            
            if (behaviourMeta == null) throw new Exception($"Could not find behaviour meta. {behaviourFile.FullPath}");

            var properties = _utility.GetSerializedProperties(behaviourFile.Object);
            RegisterBehaviour(new BehaviourAssetInfo(name, behaviourMeta.BehaviourId, behaviourFile, properties));
        }
    }

    private void RegisterBehaviour(BehaviourAssetInfo behaviourAssetInfo)
    {
        var behaviourId = behaviourAssetInfo.BehaviourId;
        if (_behaviours.ContainsKey(behaviourId))
        {
            throw new Exception($"Behaviour with id {behaviourId} already exists. Existing value: {_behaviours[behaviourId].Behaviour.FullPath}. New value: {behaviourAssetInfo.Behaviour.FullPath}");
        }
        _behaviours.Add(behaviourId, behaviourAssetInfo);
        _maxBehaviourId = Math.Max(_maxBehaviourId, behaviourId);
    }

    private Task<ObjectFile<AssetMeta>> CreateMetaFile(ObjectFile<string> behaviourFile, BehaviourMeta behaviourMeta)
    {
        var meta = new AssetMeta(_assetCreator.AllocateAssetId());
        meta.AddData(BehaviourMeta.Key, behaviourMeta);
        return _assetCreator.CreateMetaFile(meta, behaviourFile.FullPath);
    }

    private void LogBehaviours()
    {
        foreach (var behaviourAssetInfo in _behaviours.OrderBy(x => x.Value.BehaviourId))
        {
            var value = behaviourAssetInfo.Value;
            _logger.Log($"{value.BehaviourId,-3} {value.BehaviourName}");
        }
    }
}