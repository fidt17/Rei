using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal sealed class McpEditorSessionRegistration : IMcpEditorSession, IDisposable
{
    private const int MAX_ENTITY_NAME_LENGTH = 128;
    private const int MAX_BEHAVIOUR_NAME_LENGTH = 256;
    private const int MAX_PROPERTY_NAME_LENGTH = 256;

    private readonly IActiveProjectService _activeProjectService;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IEntityRenameCommand _entityRenameCommand;
    private readonly IMcpEditorAutomationService _automationService;
    private readonly IDisposable _sessionLease;
    private readonly IEntityManagementService _entityManagementService;
    private readonly IBehaviourComponentsService _behaviourComponentsService;

    public McpEditorSessionRegistration(
        IMcpEditorSessionAccessor sessionAccessor,
        IActiveProjectService activeProjectService,
        ISceneManagementService sceneManagementService,
        IBehaviourRegistry behaviourRegistry,
        IEntityRenameCommand entityRenameCommand,
        IEntityManagementService entityManagementService,
        IBehaviourComponentsService behaviourComponentsService,
        IMcpEditorAutomationService automationService)
    {
        _activeProjectService = activeProjectService;
        _sceneManagementService = sceneManagementService;
        _behaviourRegistry = behaviourRegistry;
        _entityRenameCommand = entityRenameCommand;
        _entityManagementService = entityManagementService;
        _behaviourComponentsService = behaviourComponentsService;
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

        var before = McpValueConverter.ToContractValue(property.Value);
        _behaviourComponentsService.ApplySerializedValue(property, editorValue);
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
