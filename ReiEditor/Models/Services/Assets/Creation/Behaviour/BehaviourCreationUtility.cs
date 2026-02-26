using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Creation.Behaviour;

public class BehaviourCreationUtility : IBehaviourCreationUtility
{
    public static readonly Regex ValidBehaviourNameRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    
    private readonly IResourceService _resourceService;
    private readonly IAssetCreator _assetCreator;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IMetaFilesService _metaFilesService;
    private readonly IAssetImporter _assetImporter;
    private readonly ILogger<BehaviourCreationUtility> _logger;

    public BehaviourCreationUtility(
        IResourceService resourceService,
        IAssetCreator assetCreator,
        IBehaviourRegistry behaviourRegistry,
        IMetaFilesService metaFilesService,
        IAssetImporter assetImporter,
        ILogger<BehaviourCreationUtility> logger)
    {
        _resourceService = resourceService;
        _assetCreator = assetCreator;
        _behaviourRegistry = behaviourRegistry;
        _metaFilesService = metaFilesService;
        _assetImporter = assetImporter;
        _logger = logger;
    }

    public async Task<bool> CreateBehaviourAsync(BehaviourCreationSettings settings)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.TargetDirectory) || !Directory.Exists(settings.TargetDirectory)) throw new Exception($"Target directory '{settings.TargetDirectory}' does not exist");
            if (string.IsNullOrWhiteSpace(settings.BehaviourName)) throw new Exception("Behaviour name cannot be empty");
            if (!ValidBehaviourNameRegex.IsMatch(settings.BehaviourName.Trim())) throw new Exception($"Behaviour name '{settings.BehaviourName}' is invalid");
            if (!IsBehaviourNameUnique(settings.BehaviourName)) throw new Exception($"Behaviour name '{settings.BehaviourName}' is not unique");

            var targetPath = Path.Combine(settings.TargetDirectory, $"{settings.BehaviourName}.h");
            if (_resourceService.Exists(targetPath)) throw new Exception($"Asset at '{targetPath}' already exists");

            var source = BuildBehaviourTemplate(settings.BehaviourName, settings.OverrideInit, settings.OverrideStart, settings.OverrideUpdate, settings.OverrideDispose);
            var didWrite = await _resourceService.Write(source, targetPath);
            if (!didWrite) throw new Exception($"Failed to write behaviour data to '{targetPath}'");

            var meta = new AssetMeta(_assetCreator.AllocateAssetId());
            meta.AddData(BehaviourMeta.Key, new BehaviourMeta(_behaviourRegistry.AllocateBehaviourId()));
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

    private bool IsBehaviourNameUnique(string behaviourName)
    {
        return !_behaviourRegistry.Behaviours.Values
            .Any(x => string.Equals(x.ObjectName, behaviourName, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildBehaviourTemplate(string behaviourName, bool overrideInit, bool overrideStart, bool overrideUpdate, bool overrideDispose)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#pragma once");
        builder.AppendLine("#include <Core.h>");
        builder.AppendLine();
        builder.AppendLine($"class {behaviourName} : public rei::Behaviour");
        builder.AppendLine("{");
        builder.AppendLine("private:");
        builder.AppendLine($"    BEHAVIOUR_BODY({behaviourName})");
        builder.AppendLine();
        builder.AppendLine("public:");

        AppendMethodOverride(builder, overrideInit, "Init");
        AppendMethodOverride(builder, overrideStart, "Start");
        AppendMethodOverride(builder, overrideUpdate, "Update");
        AppendMethodOverride(builder, overrideDispose, "Dispose");

        builder.AppendLine("};");

        return builder.ToString();
    }

    private static void AppendMethodOverride(StringBuilder builder, bool shouldAppend, string methodName)
    {
        if (!shouldAppend) return;

        builder.AppendLine();
        builder.AppendLine($"    void {methodName}() override");
        builder.AppendLine("    {");
        builder.AppendLine("    }");
    }
}
