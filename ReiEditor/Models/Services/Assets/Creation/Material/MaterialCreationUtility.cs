using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Creation.Material;

public class MaterialCreationUtility : IMaterialCreationUtility
{
    public static readonly Regex ValidMaterialNameRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    
    private readonly IResourceService _resourceService;
    private readonly IAssetCreator _assetCreator;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IMetaFilesService _metaFilesService;
    private readonly IAssetImporter _assetImporter;
    private readonly ILogger<MaterialCreationUtility> _logger;

    public MaterialCreationUtility(
        IResourceService resourceService,
        IAssetCreator assetCreator,
        IAssetRegistry assetRegistry,
        IMetaFilesService metaFilesService,
        IAssetImporter assetImporter,
        ILogger<MaterialCreationUtility> logger)
    {
        _resourceService = resourceService;
        _assetCreator = assetCreator;
        _assetRegistry = assetRegistry;
        _metaFilesService = metaFilesService;
        _assetImporter = assetImporter;
        _logger = logger;
    }

    public async Task<bool> CreateMaterialAsync(MaterialCreationSettings settings)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.TargetDirectory) || !Directory.Exists(settings.TargetDirectory)) throw new Exception($"Target directory '{settings.TargetDirectory}' does not exist");

            if (string.IsNullOrWhiteSpace(settings.MaterialName)) throw new Exception("Material name cannot be empty");
            if (!ValidMaterialNameRegex.IsMatch(settings.MaterialName)) throw new Exception($"Material name '{settings.MaterialName}' is invalid");
            if (!_assetRegistry.IsUniqueAssetName(settings.MaterialName, FileExtensions.MATERIAL)) throw new Exception($"Material name '{settings.MaterialName}' is not unique");
            if (!_assetRegistry.TryGetByIdAndExtensions(settings.ShaderAssetId, new[] { FileExtensions.RSHADER }, out var shaderAssetInfo)) throw new Exception($"Shader asset '{settings.ShaderAssetId}' does not exist");

            var materialPath = Path.Combine(settings.TargetDirectory, $"{settings.MaterialName}{FileExtensions.MATERIAL}");
            if (_resourceService.Exists(materialPath)) throw new Exception($"Asset at '{materialPath}' already exists");

            var materialData = BuildMaterialData(shaderAssetInfo.Meta.AssetId);
            var didWrite = await _resourceService.Write(materialData, materialPath);
            if (!didWrite) throw new Exception($"Failed to write material data to '{materialPath}'");

            var meta = new AssetMeta(_assetCreator.AllocateAssetId());
            await _metaFilesService.CreateMetaFile(meta, materialPath);
            await _assetImporter.ReimportPaths(new[] { materialPath });

            return true;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return false;
        }
    }

    private static string BuildMaterialData(string shaderAssetId)
    {
        return $"{{\n  \"shaderAssetId\": \"{shaderAssetId}\"\n}}\n";
    }
}
