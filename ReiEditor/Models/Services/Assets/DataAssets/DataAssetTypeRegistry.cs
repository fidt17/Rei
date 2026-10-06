using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Creation;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Utils.Path;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.DataAssets;

public sealed class DataAssetTypeRegistry : IDataAssetTypeRegistry
{
    private readonly object _registryLock = new();
    private Dictionary<int, DataAssetTypeInfo> _types = new();
    private int _maxTypeId = -1;

    private readonly IResourceService _resourceService;
    private readonly IMetaFilesService _metaFilesService;
    private readonly IEngineSettingsProvider _engineSettingsProvider;
    private readonly IAssetCreator _assetCreator;
    private readonly ILogger<DataAssetTypeRegistry> _logger;

    public DataAssetTypeRegistry(IResourceService resourceService, IMetaFilesService metaFilesService, IAssetCreator assetCreator, ILogger<DataAssetTypeRegistry> logger, IEngineSettingsProvider engineSettingsProvider)
    {
        _resourceService = resourceService;
        _metaFilesService = metaFilesService;
        _assetCreator = assetCreator;
        _logger = logger;
        _engineSettingsProvider = engineSettingsProvider;
    }

    public IEnumerable<DataAssetTypeInfo> GetDataAssetTypes()
    {
        lock (_registryLock)
        {
            return _types.Values.OrderBy(x => x.ObjectName).ToList();
        }
    }

    public DataAssetTypeInfo? GetDataAssetType(int dataAssetTypeId)
    {
        lock (_registryLock)
        {
            return _types.GetValueOrDefault(dataAssetTypeId);
        }
    }

    public DataAssetTypeInfo? GetDataAssetType(string objectName)
    {
        lock (_registryLock)
        {
            return _types.Values.FirstOrDefault(x => x.ObjectName == objectName);
        }
    }

    public int AllocateDataAssetTypeId()
    {
        lock (_registryLock)
        {
            _maxTypeId++;
            return _maxTypeId;
        }
    }

    public async Task RefreshAsync(IEnumerable<SerializableObjectInfo> dataAssetDeclarations)
    {
        var refreshedTypes = new Dictionary<int, DataAssetTypeInfo>();
        var missingMetadata = new List<(SerializableObjectInfo Type, AssetMeta Meta, string MetaPath)>();
        var projectRoot = Path.GetFullPath(_resourceService.GetProjectPath());
        var maxTypeId = -1;

        foreach (var declaration in dataAssetDeclarations)
        {
            var metaPath = GetMetadataPath(declaration, projectRoot);
            var meta = File.Exists(metaPath) ? await _resourceService.TryLoad<AssetMeta>(metaPath) : null;
            meta ??= new AssetMeta(_assetCreator.AllocateAssetId());

            if (!meta.TryGetData(DataAssetMeta.Key, out DataAssetMeta? dataAssetMeta))
            {
                missingMetadata.Add((declaration, meta, metaPath));
                continue;
            }

            Register(declaration, dataAssetMeta, refreshedTypes);
            maxTypeId = Math.Max(maxTypeId, dataAssetMeta.DataAssetTypeId);
        }

        foreach (var (declaration, meta, metaPath) in missingMetadata)
        {
            maxTypeId++;
            var dataAssetMeta = new DataAssetMeta(maxTypeId);
            meta.AddData(DataAssetMeta.Key, dataAssetMeta);
            await _metaFilesService.CreateMetaFile(meta, metaPath[..^FileExtensions.META.Length]);
            Register(declaration, dataAssetMeta, refreshedTypes);
        }

        lock (_registryLock)
        {
            _types = refreshedTypes;
            _maxTypeId = maxTypeId;
        }

        _logger.Log($"Total DataAsset types found: {refreshedTypes.Count}");
    }

    private string GetMetadataPath(SerializableObjectInfo declaration, string projectRoot)
    {
        if (declaration.IsTemplate)
        {
            throw new Exception($"DataAsset type cannot be templated. Type={declaration.ObjectName}, path={declaration.Source.FullPath}");
        }

        var sourcePath = Path.GetFullPath(declaration.Source.FullPath);
        if (sourcePath.IsUnderDirectory(projectRoot)) return sourcePath + FileExtensions.META;

        var engineResources = _engineSettingsProvider.GetEngineResourcesDir();
        if (!sourcePath.IsUnderDirectory(engineResources))
        {
            throw new Exception($"DataAsset type must be declared under project or engine resources directory. Type={declaration.ObjectName}, path={sourcePath}");
        }

        // Engine-owned declarations keep project-specific type IDs beside built-in behaviour metadata.
        return _resourceService.GetRootPath("Internal", Path.GetRelativePath(engineResources, sourcePath)) + FileExtensions.META;
    }

    private static void Register(SerializableObjectInfo declaration, DataAssetMeta dataAssetMeta, IDictionary<int, DataAssetTypeInfo> types)
    {
        if (dataAssetMeta.DataAssetTypeId < 0)
        {
            throw new Exception($"DataAsset type {declaration.ObjectName} has invalid id {dataAssetMeta.DataAssetTypeId}");
        }

        if (types.TryGetValue(dataAssetMeta.DataAssetTypeId, out var existing))
        {
            throw new Exception($"DataAsset type id {dataAssetMeta.DataAssetTypeId} is duplicated. Existing={existing.SerializableObject.Source.FullPath}, new={declaration.Source.FullPath}");
        }

        types.Add(dataAssetMeta.DataAssetTypeId, new DataAssetTypeInfo(dataAssetMeta.DataAssetTypeId, declaration));
    }
}
