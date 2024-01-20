using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourComponentsService : IBehaviourComponentsService
{
    private readonly Dictionary<int, BehaviourAssetInfo> _behaviours = new();
    private int _maxBehaviourId;

    private readonly BehaviourFileUtility _utility;
    private readonly IAssetCreator _assetCreator;
    private readonly ISerializer _serializer;
    private readonly IResourceService _resourceService;
    private readonly ILogger<BehaviourComponentsService> _logger;

    public BehaviourComponentsService(IResourceService resourceService, ISerializer serializer, ILogger<BehaviourComponentsService> logger, IAssetCreator assetCreator)
    {
        _utility = new BehaviourFileUtility(resourceService);
        _resourceService = resourceService;
        _serializer = serializer;
        _logger = logger;
        _assetCreator = assetCreator;
    }

    public async Task<int> ImportBehaviours()
    {
        var behaviourFiles = _utility.GetAllBehaviours();
        var metaFiles = await _utility.GetAllBehaviourMetas();
        
        RegisterBehaviours(behaviourFiles, metaFiles);
        GenerateBehaviourRegistry();

        return _behaviours.Count;
    }

    private void RegisterBehaviours(List<ObjectFile<string>> behaviourFiles, List<ObjectFile<BehaviourMeta>> metaFiles)
    {
        var newBehaviours = new List<ObjectFile<string>>();
        foreach (var behaviourFile in behaviourFiles)
        {
            if (!_utility.TryGetBehaviourNameFrom(behaviourFile.Object, out var name)) throw new Exception($"Invalid behaviour file. {behaviourFile.FullPath}");

            var metaFile = metaFiles.Find(x => x.FullPath == behaviourFile.FullPath.Replace(FileExtensions.H, FileExtensions.META));
            if (metaFile == null)
            {
                newBehaviours.Add(behaviourFile);
                continue;
            }

            RegisterBehaviour(new BehaviourAssetInfo(name, metaFile, behaviourFile));
        }
        
        foreach (var behaviourFile in newBehaviours)
        {
            if (!_utility.TryGetBehaviourNameFrom(behaviourFile.Object, out var name)) throw new Exception($"Invalid behaviour file. {behaviourFile.FullPath}");
            var metaFile = CreateMetaFile(behaviourFile, _maxBehaviourId++);
            RegisterBehaviour(new BehaviourAssetInfo(name, metaFile, behaviourFile));
        }
    }

    private void RegisterBehaviour(BehaviourAssetInfo behaviourAssetInfo)
    {
        var behaviourId = behaviourAssetInfo.Meta.Object.BehaviourId;
        if (_behaviours.ContainsKey(behaviourId)) throw new Exception($"Behaviour with id {behaviourId} already exists");
        _behaviours.Add(behaviourId, behaviourAssetInfo);
        _maxBehaviourId = Math.Max(_maxBehaviourId, behaviourId);
    }

    private ObjectFile<BehaviourMeta> CreateMetaFile(ObjectFile<string> behaviourFile, int behaviourId)
    {
        var meta = new BehaviourMeta(_assetCreator.AllocateAssetId(), AssetType.Behaviour)
        {
            BehaviourId = behaviourId
        };

        var extension = Path.GetExtension(behaviourFile.FullPath);
        var serialized = _serializer.Serialize(meta);
        var metaPath = behaviourFile.FullPath.Replace(extension, FileExtensions.META);
        File.WriteAllText(metaPath, serialized);
        return new ObjectFile<BehaviourMeta>(meta, metaPath);
    }

    private void GenerateBehaviourRegistry()
    {
        var dir = _resourceService.GetFullPath("Scripts", "Internal");
        Directory.CreateDirectory(dir);
        var text = "#include <Modules/EntityManagement/EntityManager.h>" +
                   "\nvoid ConfigureComponentsFactory(rei::BehaviourComponentFactory& factory) { }";
        File.WriteAllText(Path.Combine(dir, "BehaviourRegistry.cpp"), text);
    }
}