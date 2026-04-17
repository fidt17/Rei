using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Scenes;

public sealed class SceneAssetDropTargetBuilderService : ISceneAssetDropTargetBuilderService
{
    private readonly ILogger<SceneAssetDropTargetBuilderService> _logger;
    private readonly IAssetTypeMapper _assetTypeMapper;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IResourceService _resourceService;
    private readonly ISceneManagementService _sceneManagementService;

    public SceneAssetDropTargetBuilderService(
        ILogger<SceneAssetDropTargetBuilderService> logger,
        IAssetTypeMapper assetTypeMapper,
        IAssetRegistry assetRegistry,
        IResourceService resourceService,
        ISceneManagementService sceneManagementService)
    {
        _logger = logger;
        _assetTypeMapper = assetTypeMapper;
        _assetRegistry = assetRegistry;
        _resourceService = resourceService;
        _sceneManagementService = sceneManagementService;
    }

    public bool CanHandleAssetPaths(IReadOnlyList<string> assetPaths)
    {
        return BuildTargets(assetPaths).Count > 0;
    }

    public IReadOnlyList<SceneAssetDropTarget> BuildTargets(IReadOnlyList<string> assetPaths)
    {
        if (assetPaths.Count == 0) return Array.Empty<SceneAssetDropTarget>();

        var scene = _sceneManagementService.CurrentScene.Value;
        var existingNames = scene?.Entities.Select(x => x.Name).ToList() ?? new List<string>();
        var targets = new List<SceneAssetDropTarget>();

        foreach (var assetPath in NormalizeAssetPaths(assetPaths))
        {
            var assetType = GetSupportedAssetType(assetPath);
            if (assetType == AssetType.Unknown) continue;
            if (!TryResolveAssetId(assetPath, out var assetId)) continue;

            var baseName = Path.GetFileNameWithoutExtension(assetPath);
            var entityName = NamingUtils.GetUniqueName(baseName, existingNames.Concat(targets.Select(x => x.EntityName)));
            targets.Add(new SceneAssetDropTarget(assetPath, assetType, assetId, entityName));
        }

        return targets;
    }

    private IEnumerable<string> NormalizeAssetPaths(IReadOnlyList<string> assetPaths)
    {
        return assetPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private AssetType GetSupportedAssetType(string assetPath)
    {
        if (FileExtensions.HasAnyExtension(assetPath, _assetTypeMapper.GetExtensionsForAssetType(AssetType.Model)))
        {
            return AssetType.Model;
        }

        if (FileExtensions.HasAnyExtension(assetPath, _assetTypeMapper.GetExtensionsForAssetType(AssetType.Texture)))
        {
            return AssetType.Texture;
        }

        return AssetType.Unknown;
    }

    private bool TryResolveAssetId(string assetPath, out string assetId)
    {
        assetId = string.Empty;

        if (_assetRegistry.TryGetByPath(assetPath, out var assetInfo))
        {
            assetId = assetInfo.Meta.AssetId;
            return !string.IsNullOrWhiteSpace(assetId);
        }

        var resolved = ReiAssetIdUtility.TryCreateFromAssetPath(assetPath, _resourceService, out assetId);
        if (!resolved)
        {
            _logger.LogWarning($"Scene asset drop could not resolve asset id for path={assetPath}");
        }

        return resolved;
    }
}
