using System;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Engine.Api;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal sealed class McpEditorSessionRegistration : IMcpEditorSession, IDisposable
{
    private const int MAX_ENTITY_NAME_LENGTH = 128;
    private const int MAX_BEHAVIOUR_NAME_LENGTH = 256;
    private const int MAX_PROPERTY_NAME_LENGTH = 256;
    private const int MAX_ASSET_ID_LENGTH = 256;

    private readonly IActiveProjectService _activeProjectService;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IEntityRenameCommand _entityRenameCommand;
    private readonly IMcpEditorAutomationService _automationService;
    private readonly IDisposable _sessionLease;
    private readonly IEntityManagementService _entityManagementService;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly ISerializedPropertiesService _serializedPropertiesService;
    private readonly IAssetsService _assetsService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IShaderRegistry _shaderRegistry;
    private readonly IAssetRuntimeSyncService _assetRuntimeSyncService;
    private readonly IDataAssetService _dataAssetService;
    private readonly IDataAssetTypeRegistry _dataAssetTypeRegistry;
    private readonly IResourceService _resourceService;
    private readonly IProjectAssetFocusService _assetFocusService;
    private readonly ISelectionService _selectionService;
    private readonly IAssetRuntimeInspectionService _runtimeInspection;

    public McpEditorSessionRegistration(
        IMcpEditorSessionAccessor sessionAccessor,
        IActiveProjectService activeProjectService,
        ISceneManagementService sceneManagementService,
        IBehaviourRegistry behaviourRegistry,
        IEntityRenameCommand entityRenameCommand,
        IEntityManagementService entityManagementService,
        IBehaviourComponentsService behaviourComponentsService,
        ISerializedPropertiesService serializedPropertiesService,
        IAssetsService assetsService,
        IAssetRegistry assetRegistry,
        IShaderRegistry shaderRegistry,
        IAssetRuntimeSyncService assetRuntimeSyncService,
        IDataAssetService dataAssetService,
        IDataAssetTypeRegistry dataAssetTypeRegistry,
        IResourceService resourceService,
        IMcpEditorAutomationService automationService,
        IProjectAssetFocusService assetFocusService,
        ISelectionService selectionService,
        IAssetRuntimeInspectionService runtimeInspection)
    {
        _activeProjectService = activeProjectService;
        _sceneManagementService = sceneManagementService;
        _behaviourRegistry = behaviourRegistry;
        _entityRenameCommand = entityRenameCommand;
        _entityManagementService = entityManagementService;
        _behaviourComponentsService = behaviourComponentsService;
        _serializedPropertiesService = serializedPropertiesService;
        _assetsService = assetsService;
        _assetRegistry = assetRegistry;
        _shaderRegistry = shaderRegistry;
        _assetRuntimeSyncService = assetRuntimeSyncService;
        _dataAssetService = dataAssetService;
        _dataAssetTypeRegistry = dataAssetTypeRegistry;
        _resourceService = resourceService;
        _assetFocusService = assetFocusService;
        _selectionService = selectionService;
        _runtimeInspection = runtimeInspection;
        _automationService = automationService;
        _sessionLease = sessionAccessor.Attach(this);
    }

    public void Dispose()
    {
        _sessionLease.Dispose();
    }

    public ReiEditorState GetState()
    {
        var project = _activeProjectService.GetActiveProject();
        var scene = _sceneManagementService.CurrentScene.Value;

        return new ReiEditorState(
            scene == null ? ReiEditorStatus.PROJECT_LOADING : ReiEditorStatus.READY,
            new ReiProjectInfo(project.ProjectName, project.GetDirectoryPath(), project.ProjectFilePath, project.ProjectSolutionPath),
            scene == null ? null : new ReiSceneInfo(scene.AssetId, scene.Name, scene.Entities.Count()),
            _automationService.GetEngineInfo(),
            _automationService.GetState());
    }

    public ReiEntityList ListEntities()
    {
        var scene = GetRequiredScene();
        var entities = new List<ReiEntitySummary>();

        foreach (var rootNode in scene.Hierarchy.RootNodes)
        {
            AddNodeAndChildren(rootNode, 0, entities);
        }

        return new ReiEntityList(scene.AssetId, scene.Name, entities);
    }

    public ReiEntityDetails GetEntity(int entityId)
    {
        var entity = GetRequiredEntity(entityId);
        var behaviours = entity.Behaviours
            .Select(CreateBehaviourDetails)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .ToList();

        return new ReiEntityDetails(entity.Id, entity.Name, entity.Transform.Parent, entity.Transform.Order, behaviours);
    }

    public ReiEntityMutationResult RenameEntity(int entityId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ReiMcpOperationException("invalid_entity_name", "Entity name must not be empty.");
        }

        newName = newName.Trim();
        if (newName.Length > MAX_ENTITY_NAME_LENGTH)
        {
            throw new ReiMcpOperationException("invalid_entity_name", $"Entity name must not exceed {MAX_ENTITY_NAME_LENGTH} characters.");
        }

        var entity = GetRequiredEntity(entityId);
        if (string.Equals(entity.Name, newName, StringComparison.Ordinal))
        {
            return new ReiEntityMutationResult(false, CreateEntitySummary(entity, GetEntityDepth(entity)), "Entity already has requested name.");
        }

        _entityRenameCommand.Execute(new EntityRenameCommandTarget(entity, newName));
        if (!string.Equals(entity.Name, newName, StringComparison.Ordinal))
        {
            throw new ReiMcpOperationException("rename_failed", $"Editor did not apply name {newName} to entity {entityId}.");
        }

        return new ReiEntityMutationResult(true, CreateEntitySummary(entity, GetEntityDepth(entity)), "Entity renamed. Save project to persist change.");
    }

    public ReiBehaviourMutationResult AddBehaviour(int entityId, string behaviourName)
    {
        var behaviourInfo = GetRequiredBehaviourInfo(behaviourName);
        var entity = GetRequiredEntity(entityId);

        if (entity.HasComponent(behaviourInfo.BehaviourId))
        {
            return new ReiBehaviourMutationResult(false, GetEntity(entityId), "Entity already has requested behaviour.");
        }

        _entityManagementService.AddBehaviour(entity, behaviourInfo.BehaviourId);
        if (!entity.HasComponent(behaviourInfo.BehaviourId))
        {
            throw new ReiMcpOperationException(
                "add_behaviour_failed",
                $"Editor did not add behaviour {behaviourInfo.ObjectName} to entity {entityId}.");
        }

        return new ReiBehaviourMutationResult(true, GetEntity(entityId), "Behaviour added. Save project to persist change.");
    }

    public ReiBehaviourPropertyMutationResult SetBehaviourProperty(
        int entityId,
        string behaviourName,
        string propertyName,
        object? value)
    {
        var behaviourInfo = GetRequiredBehaviourInfo(behaviourName);
        var entity = GetRequiredEntity(entityId);
        var behaviour = entity.GetBehaviour(behaviourInfo.BehaviourId) ??
                        throw new ReiMcpOperationException(
                            "behaviour_not_attached",
                            $"Entity {entityId} does not have behaviour {behaviourInfo.ObjectName}.");

        propertyName = ValidateName(propertyName, MAX_PROPERTY_NAME_LENGTH, "property", "invalid_property_name");
        if (!behaviour.HasProperty(propertyName))
        {
            throw new ReiMcpOperationException(
                "property_not_found",
                $"Behaviour {behaviourInfo.ObjectName} does not have serialized property {propertyName}.");
        }

        var property = behaviour.GetProperty(propertyName);
        var editorValue = McpValueConverter.ToEditorValue(value);
        ValidatePropertyValue(property, editorValue);
        ValidateAssetReferenceValue(property, editorValue);

        var before = McpValueConverter.ToContractValue(property.Value);
        _serializedPropertiesService.ApplyValue(property, editorValue);
        var after = McpValueConverter.ToContractValue(property.Value);
        var changed = !ContractValuesEqual(before, after);
        var message = changed
            ? "Behaviour property changed. Save project to persist change."
            : "Behaviour property already has requested value.";

        return new ReiBehaviourPropertyMutationResult(
            changed,
            entityId,
            CreateBehaviourDetails(behaviour),
            CreatePropertyDetails(property),
            message);
    }

    public async Task<ReiMaterialPropertyMutationResult> SetMaterialPropertyAsync(
        string materialAssetId,
        string propertyName,
        object? value)
    {
        materialAssetId = ValidateAssetId(materialAssetId);
        propertyName = ValidateName(propertyName, MAX_PROPERTY_NAME_LENGTH, "Material property", "invalid_property_name");

        if (!_assetRegistry.TryGetByIdAndExtensions(materialAssetId, FileExtensions.MaterialAssetExtensions, out var assetInfo))
        {
            throw new ReiMcpOperationException(
                "material_not_found",
                "Material asset " + materialAssetId + " does not exist in current project.");
        }

        var material = await _assetsService.Load<Material>(assetInfo) ??
                       throw new ReiMcpOperationException(
                           "material_load_failed",
                           "Editor could not load material asset " + materialAssetId + ".");

        if (!_shaderRegistry.TryGetById(material.ShaderAssetId, out var shader))
        {
            throw new ReiMcpOperationException(
                "material_shader_not_found",
                "Material shader " + material.ShaderAssetId + " is not registered. Refresh assets first.");
        }

        var uniform = shader.Uniforms.SingleOrDefault(x => string.Equals(x.Name, propertyName, StringComparison.Ordinal));
        if (uniform == null || !uniform.IsSupported)
        {
            throw new ReiMcpOperationException(
                "material_property_not_found",
                "Shader " + material.ShaderAssetId + " does not have supported property " + propertyName + ".");
        }

        material.Properties.TryGetValue(propertyName, out var currentValue);
        var property = MaterialShaderPropertyUtils.CreateSerializedProperty(uniform, currentValue);
        var editorValue = McpValueConverter.ToEditorValue(value);
        ValidatePropertyValue(property, editorValue);
        _serializedPropertiesService.ApplyValue(property, editorValue);

        var normalizedValue = MaterialShaderPropertyUtils.ConvertSerializedPropertyToMaterialValue(uniform.Type, property);
        ValidateMaterialTextureReference(uniform.Type, property, normalizedValue);

        var before = McpValueConverter.ToContractValue(currentValue);
        var after = McpValueConverter.ToContractValue(normalizedValue);
        var changed = !ContractValuesEqual(before, after);
        var runtimeSynced = true;
        if (changed)
        {
            material.Properties[propertyName] = normalizedValue;
            runtimeSynced = _assetRuntimeSyncService.TrySetAssetData(materialAssetId, JsonConvert.SerializeObject(material));
        }

        var message = changed
            ? runtimeSynced
                ? "Material property changed and synchronized. Save project to persist change."
                : "Material property changed. Runtime sync unavailable; save and reload will apply it."
            : "Material property already has requested value.";

        return new ReiMaterialPropertyMutationResult(
            changed,
            materialAssetId,
            material.ShaderAssetId,
            new ReiPropertyDetails(propertyName, uniform.Type.ToString(), uniform.SourceType, after),
            runtimeSynced,
            message);
    }

    public ReiDataAssetTypeList ListDataAssetTypes()
    {
        var types = _dataAssetTypeRegistry.GetDataAssetTypes()
            .OrderBy(x => x.ObjectName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ReiDataAssetTypeDetails(
                x.TypeId,
                x.ObjectName,
                x.Namespace,
                x.SerializedProperties
                    .Where(p => !p.Value.HideInEditor)
                    .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new ReiPropertySchema(p.Key, p.Value.Type.ToString(), p.Value.SourceType))
                    .ToList()))
            .ToList();
        return new ReiDataAssetTypeList(types);
    }

    public ReiDataAssetList ListDataAssets()
    {
        var assets = new List<ReiDataAssetSummary>();
        foreach (var assetInfo in _assetRegistry.GetAllAssets().OfType<DataAssetInfo>())
        {
            var typeInfo = _dataAssetTypeRegistry.GetDataAssetType(assetInfo.DataAssetTypeId);
            if (typeInfo == null) continue;

            assets.Add(new ReiDataAssetSummary(
                assetInfo.Meta.AssetId,
                assetInfo.DataAssetTypeId,
                typeInfo.ObjectName,
                GetDataAssetProjectPath(assetInfo.FullPath)));
        }

        return new ReiDataAssetList(assets
            .OrderBy(x => x.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    public async Task<ReiDataAssetCreationResult> CreateDataAssetAsync(string typeName, string projectPath)
    {
        var typeInfo = GetRequiredDataAssetType(typeName);
        var normalizedProjectPath = ValidateDataAssetProjectPath(projectPath);
        var fullPath = _resourceService.GetProjectPath(normalizedProjectPath);
        if (File.Exists(fullPath))
        {
            throw new ReiMcpOperationException("asset_already_exists", $"Asset already exists at {normalizedProjectPath}.");
        }

        var asset = await _dataAssetService.Create(typeInfo.TypeId, normalizedProjectPath) ??
                    throw new ReiMcpOperationException(
                        "data_asset_create_failed",
                        $"Editor could not create {typeInfo.ObjectName} at {normalizedProjectPath}.");
        var details = CreateDataAssetDetails(asset, typeInfo);
        return new ReiDataAssetCreationResult(true, details, "DataAsset created. Save project after further edits.");
    }

    public ReiAssetSelection SelectAsset(string assetId)
    {
        assetId = ValidateAssetId(assetId);
        if (!_assetRegistry.TryGetById(assetId, out _))
            throw new ReiMcpOperationException("asset_not_found", $"Asset {assetId} does not exist.");
        _assetFocusService.SelectAsset(assetId);
        if (_selectionService.ActiveSelection.Value is not IAssetSelectable selected || selected.AssetId != assetId)
            throw new ReiMcpOperationException("selection_failed", $"Could not select asset {assetId}.");
        return new(assetId, selected.AssetName, selected.IsAssetSupportedInMonitor);
    }

    public async Task<ReiAssetState> GetAssetStateAsync(string assetId, string source)
    {
        assetId = ValidateAssetId(assetId);
        if (source != "editor" && source != "runtime")
            throw new ReiMcpOperationException("invalid_source", "Source must be editor or runtime.");
        if (!_assetRegistry.TryGetById(assetId, out var info))
            throw new ReiMcpOperationException("asset_not_found", $"Asset {assetId} does not exist.");
        if (source == "runtime") return _runtimeInspection.Read(assetId);
        JToken values;
        if (info is DataAssetInfo)
        {
            var asset = await _dataAssetService.Load(assetId);
            if (asset == null) return new(assetId, source, "read_failed", null);
            values = JObject.FromObject(asset.Properties.ToDictionary(x => x.Key, x => McpValueConverter.ToContractValue(x.Value.Value)));
        }
        else if (RuntimeAssetTypeResolver.TryResolveAssetType(_assetRegistry, assetId, out var type) && type == "Material")
        {
            var material = await _assetsService.Load<Material>(assetId);
            if (material == null) return new(assetId, source, "read_failed", null);
            var materialValues = JObject.FromObject(material);
            materialValues.Remove(nameof(Asset.SerializerVersion));
            values = materialValues;
        }
        else return new(assetId, source, "unsupported", null);
        using var document = System.Text.Json.JsonDocument.Parse(values.ToString(Formatting.None));
        return new(assetId, source, "loaded", document.RootElement.Clone());
    }

    public async Task<ReiDataAssetDetails> GetDataAssetAsync(string assetId)
    {
        assetId = ValidateAssetId(assetId);
        var asset = await _dataAssetService.Load(assetId) ??
                    throw new ReiMcpOperationException("data_asset_not_found", $"DataAsset {assetId} does not exist or has an unknown type.");
        var typeInfo = _dataAssetTypeRegistry.GetDataAssetType(asset.DataAssetTypeId) ??
                       throw new ReiMcpOperationException("data_asset_type_not_found", $"DataAsset {assetId} has unknown type id {asset.DataAssetTypeId}.");
        return CreateDataAssetDetails(asset, typeInfo);
    }

    public async Task<ReiDataAssetPropertyMutationResult> SetDataAssetPropertyAsync(string assetId, string propertyName, object? value)
    {
        assetId = ValidateAssetId(assetId);
        var asset = await _dataAssetService.Load(assetId) ??
                    throw new ReiMcpOperationException("data_asset_load_failed", $"Editor could not load DataAsset {assetId}.");

        propertyName = ValidateName(propertyName, MAX_PROPERTY_NAME_LENGTH, "DataAsset property", "invalid_property_name");
        if (!asset.HasProperty(propertyName))
        {
            throw new ReiMcpOperationException("property_not_found", $"DataAsset {assetId} does not have property {propertyName}.");
        }

        var property = asset.GetProperty(propertyName);
        var editorValue = McpValueConverter.ToEditorValue(value);
        ValidatePropertyValue(property, editorValue);
        ValidateAssetReferenceValue(property, editorValue);

        var before = McpValueConverter.ToContractValue(property.Value);
        if (!_dataAssetService.TrySetProperty(asset, propertyName, editorValue, out var runtimeSynced))
        {
            throw new ReiMcpOperationException("data_asset_set_failed", $"Editor could not set property {propertyName}.");
        }

        var after = McpValueConverter.ToContractValue(property.Value);
        var changed = !ContractValuesEqual(before, after);
        var typeInfo = _dataAssetTypeRegistry.GetDataAssetType(asset.DataAssetTypeId)!;
        var details = CreateDataAssetDetails(asset, typeInfo);
        var isPlayMode = string.Equals(_automationService.GetEngineInfo().Mode, "PlayMode", StringComparison.Ordinal);
        var message = changed
            ? isPlayMode
                ? "DataAsset property changed for current play session. Stop play mode, set value again, then save to persist it."
                : runtimeSynced
                    ? "DataAsset property changed and synchronized. Save project to persist change."
                    : "DataAsset property changed. Runtime sync unavailable; save and reload will apply it."
            : "DataAsset property already has requested value.";

        return new ReiDataAssetPropertyMutationResult(changed, details, CreatePropertyDetails(property), runtimeSynced, message);
    }

    public Task<ReiProjectSaveResult> SaveProjectAsync() => _automationService.SaveProjectAsync();

    public ReiOperationInfo StartAssetRefresh() => _automationService.StartAssetRefresh();

    public ReiOperationInfo StartBuild(ReiBuildOptions options) => _automationService.StartBuild(options);

    public ReiOperationInfo StartPlaymode() => _automationService.StartPlaymode();

    public ReiOperationInfo StopPlaymode() => _automationService.StopPlaymode();

    public ReiOperationInfo GetOperation(string operationId) => _automationService.GetOperation(operationId);

    public ReiOperationInfo CancelOperation(string operationId) => _automationService.CancelOperation(operationId);

    public ReiLogList GetLogs(string? operationId, string minimumLevel, int limit) => _automationService.GetLogs(operationId, minimumLevel, limit);

    public Task<ReiFrameCapture> CaptureFrameAsync(CancellationToken cancellationToken) => _automationService.CaptureFrameAsync(cancellationToken);

    private Scene GetRequiredScene()
    {
        return _sceneManagementService.CurrentScene.Value ??
               throw new ReiMcpOperationException("scene_unavailable", "Project is still loading or has no active scene.");
    }

    private GameEntity GetRequiredEntity(int entityId)
    {
        return GetRequiredScene().GetById(entityId) ??
               throw new ReiMcpOperationException("entity_not_found", $"Entity {entityId} does not exist in current scene.");
    }

    private void AddNodeAndChildren(HierarchyNode<GameEntity> node, int depth, ICollection<ReiEntitySummary> result)
    {
        result.Add(CreateEntitySummary(node.Content, depth));
        foreach (var childNode in node.ChildNodes)
        {
            AddNodeAndChildren(childNode, depth + 1, result);
        }
    }

    private ReiEntitySummary CreateEntitySummary(GameEntity entity, int depth)
    {
        var behaviours = entity.Behaviours
            .Select(x => new ReiBehaviourSummary(x.Id, GetBehaviourName(x.Id)))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .ToList();

        return new ReiEntitySummary(entity.Id, entity.Name, entity.Transform.Parent, entity.Transform.Order, depth, behaviours);
    }

    private ReiBehaviourDetails CreateBehaviourDetails(BehaviourComponent behaviour)
    {
        var properties = behaviour.Properties.Values
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CreatePropertyDetails)
            .ToList();

        return new ReiBehaviourDetails(behaviour.Id, GetBehaviourName(behaviour.Id), properties);
    }

    private static ReiPropertyDetails CreatePropertyDetails(SerializedProperty property)
    {
        return new ReiPropertyDetails(
            property.Name,
            property.Type.ToString(),
            property.SourceType,
            McpValueConverter.ToContractValue(property.Value));
    }

    private BehaviourAssetInfo GetRequiredBehaviourInfo(string behaviourName)
    {
        behaviourName = ValidateName(
            behaviourName,
            MAX_BEHAVIOUR_NAME_LENGTH,
            "Behaviour",
            "invalid_behaviour_name");

        var matches = _behaviourRegistry.Behaviours.Values
            .Where(x => string.Equals(x.ObjectName, behaviourName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ReiMcpOperationException(
                "behaviour_not_found",
                $"Behaviour {behaviourName} is not registered. Refresh assets after adding behaviour source."),
            _ => throw new ReiMcpOperationException(
                "ambiguous_behaviour_name",
                $"More than one registered behaviour is named {behaviourName}.")
        };
    }

    private DataAssetTypeInfo GetRequiredDataAssetType(string typeName)
    {
        typeName = ValidateName(typeName, MAX_BEHAVIOUR_NAME_LENGTH, "DataAsset type", "invalid_data_asset_type");
        var matches = _dataAssetTypeRegistry.GetDataAssetTypes()
            .Where(x => string.Equals(x.ObjectName, typeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ReiMcpOperationException(
                "data_asset_type_not_found",
                $"DataAsset type {typeName} is not registered. Add DATA_ASSET_BODY and refresh assets first."),
            _ => throw new ReiMcpOperationException(
                "ambiguous_data_asset_type",
                $"More than one registered DataAsset type is named {typeName}.")
        };
    }

    private ReiDataAssetDetails CreateDataAssetDetails(DataAsset asset, DataAssetTypeInfo typeInfo)
    {
        if (!_assetRegistry.TryGetById(asset.AssetId, out var assetInfo))
        {
            throw new ReiMcpOperationException("data_asset_not_registered", $"DataAsset {asset.AssetId} is not registered.");
        }

        var properties = asset.Properties.Values
            .Where(property =>
                !typeInfo.SerializedProperties.TryGetValue(property.Name, out var schema) ||
                !schema.HideInEditor)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CreatePropertyDetails)
            .ToList();
        return new ReiDataAssetDetails(
            asset.AssetId,
            asset.DataAssetTypeId,
            typeInfo.ObjectName,
            GetDataAssetProjectPath(assetInfo.FullPath),
            properties);
    }

    private string GetDataAssetProjectPath(string fullPath)
    {
        return Path.GetRelativePath(_resourceService.GetProjectPath(), fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private string ValidateDataAssetProjectPath(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ReiMcpOperationException("invalid_asset_path", "DataAsset project path must not be empty.");
        }

        projectPath = projectPath.Trim();
        if (Path.IsPathRooted(projectPath))
        {
            throw new ReiMcpOperationException("invalid_asset_path", "DataAsset project path must be relative to Project directory.");
        }

        if (!Path.GetExtension(projectPath).Equals(FileExtensions.ASSET, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReiMcpOperationException("invalid_asset_path", $"DataAsset project path must end with {FileExtensions.ASSET}.");
        }

        var root = _resourceService.GetProjectPath();
        var fullPath = Path.GetFullPath(Path.Combine(root, projectPath));
        var relativePath = Path.GetRelativePath(root, fullPath);
        if (relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ReiMcpOperationException("invalid_asset_path", "DataAsset project path must stay inside Project directory.");
        }

        return relativePath;
    }

    private void ValidateAssetReferenceValue(SerializedProperty property, object? value)
    {
        if (property.Type != SerializedTypeEnum.Custom ||
            SerializedTypeNameParser.GetBaseTypeName(property.SourceType) != "AssetRef")
        {
            return;
        }

        if (value is not JObject objectValue)
        {
            throw CreateInvalidPropertyValueException(property, "Expected AssetRef object with string Id.");
        }

        var assetId = objectValue["Id"]?.Value<string>();
        if (assetId == null)
        {
            throw CreateInvalidPropertyValueException(property, "Expected AssetRef object with string Id.");
        }

        if (assetId.Length == 0) return;

        var referencedTypeName = property.TemplateTypeName ?? SourceFilesUtility.GetTemplateTypeName(property.SourceType);
        if (referencedTypeName == "Material")
        {
            if (_assetRegistry.TryGetByIdAndExtensions(assetId, FileExtensions.MaterialAssetExtensions, out _)) return;
            throw CreateInvalidPropertyValueException(property, $"Material asset {assetId} does not exist.");
        }

        var dataAssetType = referencedTypeName == null
            ? null
            : _dataAssetTypeRegistry.GetDataAssetType(referencedTypeName);
        if (dataAssetType == null) return;
        if (_dataAssetService.TryGetDataAssetTypeId(assetId, out var actualTypeId) &&
            actualTypeId == dataAssetType.TypeId)
        {
            return;
        }

        throw CreateInvalidPropertyValueException(
            property,
            $"Asset {assetId} is not a {dataAssetType.ObjectName} DataAsset.");
    }

    private static string ValidateName(string value, int maximumLength, string label, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReiMcpOperationException(errorCode, $"{label} name must not be empty.");
        }

        value = value.Trim();
        if (value.Length > maximumLength)
        {
            throw new ReiMcpOperationException(errorCode, $"{label} name must not exceed {maximumLength} characters.");
        }

        return value;
    }

    private static string ValidateAssetId(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            throw new ReiMcpOperationException("invalid_asset_id", "Asset id must not be empty.");
        }

        assetId = assetId.Trim();
        if (assetId.Length > MAX_ASSET_ID_LENGTH)
        {
            throw new ReiMcpOperationException(
                "invalid_asset_id",
                "Asset id must not exceed " + MAX_ASSET_ID_LENGTH + " characters.");
        }

        return assetId;
    }

    private void ValidateMaterialTextureReference(
        ShaderUniformType uniformType,
        SerializedProperty property,
        object? value)
    {
        if (uniformType != ShaderUniformType.Texture) return;

        var token = value == null ? null : JToken.FromObject(value);
        var textureAssetId = token?["Id"]?.Value<string>();
        if (textureAssetId == null)
        {
            throw CreateInvalidPropertyValueException(property, "Expected texture value with string Id.");
        }

        if (textureAssetId.Length == 0) return;
        if (_assetRegistry.TryGetByIdAndExtensions(textureAssetId, FileExtensions.TextureAssetExtensions, out _)) return;

        throw CreateInvalidPropertyValueException(
            property,
            "Texture asset " + textureAssetId + " does not exist in current project.");
    }

    private static void ValidatePropertyValue(SerializedProperty property, object? value)
    {
        if (property.Type == SerializedTypeEnum.Custom)
        {
            if (value is not JObject objectValue)
            {
                throw CreateInvalidPropertyValueException(property, "Expected a JSON object.");
            }

            if (property.Value is not IReadOnlyDictionary<string, SerializedProperty> nestedProperties)
            {
                throw CreateInvalidPropertyValueException(property, "Editor has no nested property schema.");
            }

            foreach (var nestedValue in objectValue.Properties())
            {
                if (!nestedProperties.TryGetValue(nestedValue.Name, out var nestedProperty))
                {
                    throw CreateInvalidPropertyValueException(property, $"Unknown nested property {nestedValue.Name}.");
                }

                ValidatePropertyValue(nestedProperty, McpValueConverter.ToEditorValue(nestedValue.Value));
            }

            return;
        }

        if (property.Type == SerializedTypeEnum.Collection && value is not JArray)
        {
            throw CreateInvalidPropertyValueException(property, "Expected a JSON array.");
        }

        if (!property.Type.IsValidValue(value))
        {
            throw CreateInvalidPropertyValueException(
                property,
                $"Value is incompatible with serialized type {property.Type} ({property.SourceType}).");
        }
    }

    private static ReiMcpOperationException CreateInvalidPropertyValueException(SerializedProperty property, string reason)
    {
        return new ReiMcpOperationException("invalid_property_value", $"Cannot set property {property.Name}. {reason}");
    }

    private static bool ContractValuesEqual(object? left, object? right)
    {
        if (left == null || right == null) return left == null && right == null;
        return JToken.DeepEquals(JToken.FromObject(left), JToken.FromObject(right));
    }

    private string GetBehaviourName(int behaviourId)
    {
        return _behaviourRegistry.TryGetById(behaviourId, out var behaviour)
            ? behaviour.ObjectName
            : $"Behaviour {behaviourId}";
    }

    private int GetEntityDepth(GameEntity entity)
    {
        var node = GetRequiredScene().Hierarchy.GetNode(entity);
        var depth = 0;

        while (node?.Parent != null)
        {
            depth++;
            node = node.Parent;
        }

        return depth;
    }
}
