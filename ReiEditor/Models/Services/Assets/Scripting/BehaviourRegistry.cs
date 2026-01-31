using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Scripting;

public class BehaviourRegistry : IBehaviourRegistry
{
    public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours => _behaviours;
    
    private int _maxBehaviourId = -1;
    private readonly object _behaviourIdLock = new();
    
    private readonly Dictionary<int, BehaviourAssetInfo> _behaviours = new();
    private readonly Dictionary<string, BehaviourAssetInfo> _behavioursByName = new();
    
    private readonly IBehaviourFileUtility _utility;
    private readonly IAssetCreator _assetCreator;
    private readonly IMetaFilesService _metaFilesService;
    private readonly BehaviourRegistrySourceGenerator _behaviourRegistrySourceGenerator;
    private readonly ILogger<BehaviourRegistry> _logger;
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;
    private readonly ISolutionGenerator _solutionGenerator;
    private readonly IActiveProjectService _activeProjectService;
    private readonly IResourceService _resourceService;
    private readonly SourceFilesUtility _sourceFilesUtility;

    public BehaviourRegistry(
        IAssetCreator assetCreator,
        IResourceService resourceService,
        ILogger<BehaviourRegistry> logger,
        ISerializableObjectsRegistry serializableObjectsRegistry,
        ISolutionGenerator solutionGenerator,
        IActiveProjectService activeProjectService, 
        SourceFilesUtility sourceFilesUtility,
        IBehaviourFileUtility behaviourFileUtility, 
        IMetaFilesService metaFilesService)
    {
        _assetCreator = assetCreator;
        _resourceService = resourceService;
        _logger = logger;
        _serializableObjectsRegistry = serializableObjectsRegistry;
        _solutionGenerator = solutionGenerator;
        _activeProjectService = activeProjectService;
        _sourceFilesUtility = sourceFilesUtility;
        _utility = behaviourFileUtility;
        _metaFilesService = metaFilesService;
        _behaviourRegistrySourceGenerator = new BehaviourRegistrySourceGenerator(resourceService, serializableObjectsRegistry);
    }

    public bool TryGetById(int id, [NotNullWhen(returnValue: true)] out BehaviourAssetInfo? behaviour) => _behaviours.TryGetValue(id, out behaviour);
    
    public int? GetIdByName(string name)
    {
        if (_behavioursByName.TryGetValue(name, out var value)) return value.BehaviourId;
        return null;
    }

    public int AllocateBehaviourId()
    {
        lock (_behaviourIdLock)
        {
            _maxBehaviourId++;
            return _maxBehaviourId;
        }
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
        await UpdateSolutionFile();
        
        foreach (var behaviourAssetInfo in _behaviours)
        {
            _behavioursByName.Add(behaviourAssetInfo.Value.ObjectName, behaviourAssetInfo.Value);
        }
        
        _logger.Log($"Total behaviours found: {_behaviours.Count}");
    }

    private async Task RegisterBehaviours(List<BehaviourFileUtility.BehaviourPathData> behaviourFiles, List<ObjectFile<AssetMeta>> metaFiles)
    {
        void registerBehaviour(BehaviourFileUtility.BehaviourPathData behaviourFile, string name, BehaviourMeta behaviourMeta)
        {
            if (behaviourMeta == null) throw new Exception($"Could not find behaviour meta. {behaviourFile.Path}");
            
            var namespaceStr = SourceFilesUtility.GetObjectNamespaceFrom(behaviourFile.Content, behaviourFile.Path);
            var properties = _sourceFilesUtility.GetSerializedProperties(behaviourFile.Content);
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
        
        return _metaFilesService.CreateMetaFile(meta, behaviourFile.MetaPath.Replace(FileExtensions.META, ""));
    }

    private async Task UpdateSolutionFile()
    {
        var scriptsPath = _resourceService.GetScriptsPath();
        var compileIncludes = new List<string>();
        
        foreach (var file in Directory.EnumerateFiles(scriptsPath, "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".h") && !file.EndsWith(".cpp")) continue;
            compileIncludes.Add(file.Replace(scriptsPath, ""));
        }

        await _solutionGenerator.AddSourceFiles(_activeProjectService.GetActiveProject().ProjectVisualStudioProjectPath, compileIncludes);
    }
}
