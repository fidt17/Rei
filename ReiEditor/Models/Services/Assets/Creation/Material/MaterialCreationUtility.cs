using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Creation.Material;

public class MaterialCreationUtility : IMaterialCreationUtility
{
    public static readonly Regex ValidMaterialNameRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    
    private readonly IResourceService _resourceService;
    private readonly IAssetCreator _assetCreator;
    private readonly IAssetRegistry _assetRegistry;
    private readonly ILogger<MaterialCreationUtility> _logger;

    public MaterialCreationUtility(
        IResourceService resourceService,
        IAssetCreator assetCreator,
        IAssetRegistry assetRegistry,
        ILogger<MaterialCreationUtility> logger)
    {
        _resourceService = resourceService;
        _assetCreator = assetCreator;
        _assetRegistry = assetRegistry;
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

            var projectRelativeMaterialPath = Path.GetRelativePath(_resourceService.GetProjectPath(), materialPath);
            if (projectRelativeMaterialPath.StartsWith("..", StringComparison.Ordinal)) throw new Exception($"Material path '{materialPath}' is outside of project directory");

            var material = new global::ReiEditor.Models.Services.Render.Material(shaderAssetInfo.Meta.AssetId);
            var didCreate = await _assetCreator.Create(material, projectRelativeMaterialPath);
            if (!didCreate) throw new Exception($"Failed to create material asset at '{materialPath}'");

            return true;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return false;
        }
    }
}
