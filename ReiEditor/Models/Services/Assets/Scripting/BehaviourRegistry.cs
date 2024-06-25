using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Scripting;

public class BehaviourRegistry : IBehaviourRegistry
{
    public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours => _behaviours;
    
    private int _maxBehaviourId = -1;
    
    private readonly Dictionary<int, BehaviourAssetInfo> _behaviours = new();
    private readonly Dictionary<string, BehaviourAssetInfo> _behavioursByName = new();
    
    private readonly BehaviourFileUtility _utility;
    private readonly IAssetCreator _assetCreator;
    private readonly BehaviourRegistrySourceGenerator _behaviourRegistrySourceGenerator;
    private readonly ILogger<BehaviourRegistry> _logger;
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;

    public BehaviourRegistry(IAssetCreator assetCreator, IResourceService resourceService, ILogger<BehaviourRegistry> logger, IEngineSettingsProvider engineSettingsProvider, ISerializableObjectsRegistry serializableObjectsRegistry)
    {
        _assetCreator = assetCreator;
        _logger = logger;
        _serializableObjectsRegistry = serializableObjectsRegistry;
        _utility = new BehaviourFileUtility(resourceService, engineSettingsProvider);
        _behaviourRegistrySourceGenerator = new BehaviourRegistrySourceGenerator(resourceService);
    }

    public bool TryGetById(int id, [NotNullWhen(returnValue: true)] out BehaviourAssetInfo? behaviour) => _behaviours.TryGetValue(id, out behaviour);
    
    public int? GetIdByName(string name)
    {
        if (_behavioursByName.TryGetValue(name, out var value)) return value.BehaviourId;
        return null;
    }

    public async Task RefreshBehaviours()
    {
        _logger.Log("Refreshing behaviours...");
        
        await _serializableObjectsRegistry.Refresh();
        
        _behaviours.Clear();
        _behavioursByName.Clear();
        
        var behaviourFiles = _utility.GetAllBehaviours();
        var metaFiles = await _utility.GetAllBehaviourMetas();

        await RegisterBehaviours(behaviourFiles, metaFiles);
        await _behaviourRegistrySourceGenerator.GenerateBehaviourRegistrySourceFile(_behaviours, _serializableObjectsRegistry.GetObjects());
        
        foreach (var behaviourAssetInfo in _behaviours)
        {
            _behavioursByName.Add(behaviourAssetInfo.Value.ObjectName, behaviourAssetInfo.Value);
        }
        
        //LogBehaviours();
        _logger.Log($"Total behaviours found: {_behaviours.Count}");
    }

    private async Task RegisterBehaviours(List<BehaviourFileUtility.BehaviourPathData> behaviourFiles, List<ObjectFile<AssetMeta>> metaFiles)
    {
        void registerBehaviour(BehaviourFileUtility.BehaviourPathData behaviourFile, string name, BehaviourMeta behaviourMeta)
        {
            if (behaviourMeta == null) throw new Exception($"Could not find behaviour meta. {behaviourFile.Path}");
            
            var namespaceStr = SourceFilesUtility.GetObjectNamespaceFrom(behaviourFile.Content);
            var properties = SourceFilesUtility.GetSerializedProperties(behaviourFile.Content);
            RegisterBehaviour(new BehaviourAssetInfo(namespaceStr, name, behaviourMeta.BehaviourId, new ObjectFile<string>(behaviourFile.Content, behaviourFile.Path), properties, behaviourFile.Path));
        }

        var newBehaviours = new List<BehaviourFileUtility.BehaviourPathData>();

        foreach (var behaviourFile in behaviourFiles)
        {
            try
            {
                if (!_utility.TryGetBehaviourNameFrom(behaviourFile.Content, out var name)) throw new Exception($"Invalid behaviour file. {behaviourFile.Path}");

                var metaFile = metaFiles.Find(x => x.FullPath == behaviourFile.MetaPath);

                if (metaFile == null || !metaFile.Object.TryGetData(BehaviourMeta.Key, out BehaviourMeta? behaviourMeta))
                {
                    newBehaviours.Add(behaviourFile);
                    continue;
                }

                registerBehaviour(behaviourFile, name, behaviourMeta);
            }
            catch (Exception e)
            {
                throw new Exception($"Behaviour registration exception. {behaviourFile.Path}.\n{e}");
            }
        }
        
        foreach (var behaviourFile in newBehaviours)
        {
            try
            {
                if (!_utility.TryGetBehaviourNameFrom(behaviourFile.Content, out var name)) throw new Exception($"Invalid behaviour file. {behaviourFile.Path}");
            
                var behaviourMeta = new BehaviourMeta(_maxBehaviourId + 1);
                await CreateMetaFile(behaviourFile, behaviourMeta);
                
                registerBehaviour(behaviourFile, name, behaviourMeta);
            }
            catch (Exception e)
            {
                throw new Exception($"Behaviour registration exception. {behaviourFile.Path}.\n{e}");
            }
        }
    }

    private void RegisterBehaviour(BehaviourAssetInfo behaviourAssetInfo)
    {
        var behaviourId = behaviourAssetInfo.BehaviourId;
        if (_behaviours.ContainsKey(behaviourId))
        {
            throw new Exception($"Behaviour with id {behaviourId} already exists. Existing value: {_behaviours[behaviourId].Source.FullPath}. New value: {behaviourAssetInfo.Source.FullPath}");
        }
        _behaviours.Add(behaviourId, behaviourAssetInfo);
        _maxBehaviourId = Math.Max(_maxBehaviourId, behaviourId);
    }

    private Task<ObjectFile<AssetMeta>> CreateMetaFile(BehaviourFileUtility.BehaviourPathData behaviourFile, BehaviourMeta behaviourMeta)
    {
        var meta = new AssetMeta(_assetCreator.AllocateAssetId());
        meta.AddData(BehaviourMeta.Key, behaviourMeta);
        return _assetCreator.CreateMetaFile(meta, behaviourFile.MetaPath.Replace(FileExtensions.META, ""));
    }

    /*
    private void LogBehaviours()
    {
        foreach (var behaviourAssetInfo in _behaviours.OrderBy(x => x.Value.BehaviourId))
        {
            var value = behaviourAssetInfo.Value;
            _logger.Log($"{value.BehaviourId,-3} {value.BehaviourName}");
        }
    }
*/
}