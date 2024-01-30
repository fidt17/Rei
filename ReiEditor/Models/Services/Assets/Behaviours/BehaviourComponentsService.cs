using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourComponentsService : IBehaviourComponentsService
{
    public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours => _behaviours;

    private readonly Dictionary<int, BehaviourAssetInfo> _behaviours = new();
    private int _maxBehaviourId = -1;

    private readonly BehaviourFileUtility _utility;
    private readonly IAssetCreator _assetCreator;
    private readonly IResourceService _resourceService;
    private readonly BehaviourRegistrySourceGenerator _behaviourRegistrySourceGenerator;
    private readonly ILogger<BehaviourComponentsService> _logger;

    public BehaviourComponentsService(IResourceService resourceService, IAssetCreator assetCreator, ILogger<BehaviourComponentsService> logger)
    {
        _utility = new BehaviourFileUtility(resourceService);
        _resourceService = resourceService;
        _assetCreator = assetCreator;
        _logger = logger;
        _behaviourRegistrySourceGenerator = new BehaviourRegistrySourceGenerator(resourceService);
    }

    public BehaviourAssetInfo? GetBehaviourById(int id)
    {
        if (_behaviours.ContainsKey(id)) return _behaviours[id];
        
        return null;
    }

    public async Task<int> ImportBehaviours()
    {
        _logger.Log("Import behaviours");
        
        _behaviours.Clear();
        
        var behaviourFiles = _utility.GetAllBehaviours();
        var metaFiles = await _utility.GetAllBehaviourMetas();
        
        await RegisterBehaviours(behaviourFiles, metaFiles);
        GenerateBehaviourRegistrySourceFile();

        return _behaviours.Count;
    }

    public bool AddComponent(GameEntity e, BehaviourComponent component)
    {
        if (!_behaviours.ContainsKey(component.Id))
        {
            _logger.LogError($"Component with ID {component} has not been registered.");
            return false;
        }

        var componentInfo = _behaviours[component.Id];
        
        if (e.HasComponent(component.Id))
        {
            _logger.LogError($"{e} already has a component {componentInfo.BehaviourId}:{componentInfo.BehaviourName}");
            return false;
        }
        
        e.AddBehaviour(component);

        var i = 0;
        foreach (var sp in componentInfo.SerializedProperties)
        {
            SetPropertyValue(e, component, sp, i++.ToString());
        }
        
        return true;
    }

    public void SetPropertyValue(GameEntity e, BehaviourComponent component, string propertyName, object value)
    {
        component.SerializedData[propertyName] = value;
    }

    public bool DeleteComponent(GameEntity e, BehaviourComponent component)
    {
        if (!e.HasBehaviour(component))
        {
            _logger.LogError($"Cannot delete component {component.Id} from {e}. Entity does not have one.");
            return false;
        }
        
        e.DeleteBehaviour(component);
        return true;
    }

    private async Task RegisterBehaviours(List<ObjectFile<string>> behaviourFiles, List<ObjectFile<AssetMeta>> metaFiles)
    {
        foreach (var behaviourFile in behaviourFiles)
        {
            if (!_utility.TryGetBehaviourNameFrom(behaviourFile.Object, out var name)) throw new Exception($"Invalid behaviour file. {behaviourFile.FullPath}");

            var metaFile = metaFiles.Find(x => x.FullPath == behaviourFile.FullPath + FileExtensions.META);

            if (metaFile == null || !metaFile.Object.TryGetData(BehaviourMeta.Key, out BehaviourMeta? behaviourMeta))
            {
                behaviourMeta = new BehaviourMeta(_maxBehaviourId + 1);
                await CreateMetaFile(behaviourFile, behaviourMeta);
            }

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

    private void GenerateBehaviourRegistrySourceFile()
    {
        _logger.Log("Generate behaviour registry source file");
        
        var dir = _resourceService.GetProjectPath("Scripts", "Internal");
        Directory.CreateDirectory(dir);

        var source = _behaviourRegistrySourceGenerator.Generate(_behaviours);
        File.WriteAllText(Path.Combine(dir, "BehaviourRegistry.cpp"), source);
    }
}