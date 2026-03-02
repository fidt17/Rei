using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Models.Services.Assets.Shaders;

public class ShaderRegistry : IShaderRegistry
{
    public IReadOnlyDictionary<string, Shader> Shaders => _shadersById;

    private readonly Dictionary<string, Shader> _shadersById = new();
    private readonly IAssetRegistry _assetRegistry;
    private readonly IShaderUniformParser _shaderUniformParser;
    private readonly ILogger<ShaderRegistry> _logger;

    public ShaderRegistry(
        IAssetRegistry assetRegistry,
        IShaderUniformParser shaderUniformParser,
        ILogger<ShaderRegistry> logger)
    {
        _assetRegistry = assetRegistry;
        _shaderUniformParser = shaderUniformParser;
        _logger = logger;
    }

    public bool TryGetById(string assetId, [NotNullWhen(returnValue: true)] out Shader? shader)
    {
        return _shadersById.TryGetValue(assetId, out shader);
    }

    public Task RefreshShaders()
    {
        _logger.Log($"Refreshing shaders...");
        
        _shadersById.Clear();

        foreach (var asset in _assetRegistry.GetAllAssetsByExtensions(new[] { FileExtensions.RSHADER }))
        {
            try
            {
                if (!File.Exists(asset.FullPath)) continue;

                var source = File.ReadAllText(asset.FullPath);
                var uniforms = _shaderUniformParser.ParseUniforms(source);

                var shader = new Shader();
                shader.SetAssetInfo(asset);
                shader.SetName(Path.GetFileNameWithoutExtension(asset.FullPath));
                shader.SetUniforms(uniforms);

                _shadersById[asset.Meta.AssetId] = shader;
            }
            catch (System.Exception e)
            {
                _logger.LogError($"Failed to process shader asset {asset.FullPath}. {e.Message}");
            }
        }
        
        _logger.Log($"Total shaders found: {_shadersById.Count}");

        return Task.CompletedTask;
    }
}
