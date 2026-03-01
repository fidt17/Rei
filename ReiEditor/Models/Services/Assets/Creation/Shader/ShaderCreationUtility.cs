using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets.Creation.Shader;

public class ShaderCreationUtility : IShaderCreationUtility
{
    public static readonly Regex ValidShaderNameRegex = new("^[A-Za-z_][A-Za-z0-9_ ]*$", RegexOptions.Compiled);

    private readonly IResourceService _resourceService;
    private readonly IAssetCreator _assetCreator;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IMetaFilesService _metaFilesService;
    private readonly IAssetImporter _assetImporter;
    private readonly IProjectTemplateProvider _projectTemplateProvider;
    private readonly ILogger<ShaderCreationUtility> _logger;

    public ShaderCreationUtility(
        IResourceService resourceService,
        IAssetCreator assetCreator,
        IAssetRegistry assetRegistry,
        IMetaFilesService metaFilesService,
        IAssetImporter assetImporter,
        IProjectTemplateProvider projectTemplateProvider,
        ILogger<ShaderCreationUtility> logger)
    {
        _resourceService = resourceService;
        _assetCreator = assetCreator;
        _assetRegistry = assetRegistry;
        _metaFilesService = metaFilesService;
        _assetImporter = assetImporter;
        _projectTemplateProvider = projectTemplateProvider;
        _logger = logger;
    }

    public async Task<bool> CreateShaderAsync(ShaderCreationSettings settings)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.TargetDirectory) || !Directory.Exists(settings.TargetDirectory))
                throw new Exception($"Target directory '{settings.TargetDirectory}' does not exist");

            if (string.IsNullOrWhiteSpace(settings.ShaderName))
                throw new Exception("Shader name cannot be empty");

            if (!ValidShaderNameRegex.IsMatch(settings.ShaderName))
                throw new Exception($"Shader name '{settings.ShaderName}' is invalid");

            if (!_assetRegistry.IsUniqueAssetName(settings.ShaderName, FileExtensions.RSHADER))
                throw new Exception($"Shader name '{settings.ShaderName}' is not unique");

            var targetPath = Path.Combine(settings.TargetDirectory, $"{settings.ShaderName}{FileExtensions.RSHADER}");
            if (_resourceService.Exists(targetPath))
                throw new Exception($"Asset at '{targetPath}' already exists");

            var shaderTemplate = await _projectTemplateProvider.GetNewShaderTemplate();
            var didWrite = await _resourceService.Write(shaderTemplate, targetPath);
            if (!didWrite)
                throw new Exception($"Failed to write shader data to '{targetPath}'");

            var meta = new AssetMeta(_assetCreator.AllocateAssetId());
            await _metaFilesService.CreateMetaFile(meta, targetPath);
            await _assetImporter.ReimportPaths(new[] { targetPath });

            return true;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return false;
        }
    }
}
