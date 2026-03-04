using System;
using System.Collections.Generic;
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

            var targetHeaderPath = Path.Combine(settings.TargetDirectory, $"{settings.BehaviourName}.h");
            var targetSourcePath = Path.Combine(settings.TargetDirectory, $"{settings.BehaviourName}.cpp");
            if (_resourceService.Exists(targetHeaderPath)) throw new Exception($"Asset at '{targetHeaderPath}' already exists");
            if (_resourceService.Exists(targetSourcePath)) throw new Exception($"Asset at '{targetSourcePath}' already exists");

            var methods = GetOverrideMethods(settings.OverrideInit, settings.OverrideStart, settings.OverrideUpdate, settings.OverrideDispose).ToArray();

            var headerSource = BuildBehaviourHeaderTemplate(settings.BehaviourName, methods);
            var didWriteHeader = await _resourceService.Write(headerSource, targetHeaderPath);
            if (!didWriteHeader) throw new Exception($"Failed to write behaviour data to '{targetHeaderPath}'");

            var cppSource = BuildBehaviourCppTemplate(settings.BehaviourName, methods);
            var didWriteSource = await _resourceService.Write(cppSource, targetSourcePath);
            if (!didWriteSource) throw new Exception($"Failed to write behaviour data to '{targetSourcePath}'");

            var meta = new AssetMeta(_assetCreator.AllocateAssetId());
            meta.AddData(BehaviourMeta.Key, new BehaviourMeta(_behaviourRegistry.AllocateBehaviourId()));
            await _metaFilesService.CreateMetaFile(meta, targetHeaderPath);
            await _assetImporter.ReimportPaths(new[] { targetHeaderPath, targetSourcePath });

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

    private static string BuildBehaviourHeaderTemplate(string behaviourName, string[] methods)
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

        AppendMethodDeclarations(builder, methods);

        builder.AppendLine("};");

        return builder.ToString();
    }

    private static string BuildBehaviourCppTemplate(string behaviourName, string[] methods)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"#include \"{behaviourName}.h\"");

        if (methods.Length == 0)
        {
            return builder.ToString();
        }

        builder.AppendLine();
        foreach (var methodName in methods)
        {
            builder.AppendLine($"void {behaviourName}::{methodName}()");
            builder.AppendLine("{");
            builder.AppendLine("}");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendMethodDeclarations(StringBuilder builder, string[] methods)
    {
        foreach (var methodName in methods)
        {
            builder.AppendLine();
            builder.AppendLine($"    void {methodName}() override;");
        }
    }

    private static IEnumerable<string> GetOverrideMethods(bool overrideInit, bool overrideStart, bool overrideUpdate, bool overrideDispose)
    {
        if (overrideInit) yield return "Init";
        if (overrideStart) yield return "Start";
        if (overrideUpdate) yield return "Update";
        if (overrideDispose) yield return "Dispose";
    }
}
