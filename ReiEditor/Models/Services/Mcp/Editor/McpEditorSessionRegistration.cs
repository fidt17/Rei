using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal sealed class McpEditorSessionRegistration : IMcpEditorSession, IDisposable
{
    private const int MAX_ENTITY_NAME_LENGTH = 128;

    private readonly IActiveProjectService _activeProjectService;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IEntityRenameCommand _entityRenameCommand;
    private readonly IAssetsService _assetsService;
    private readonly ISceneStateSynchronizer _sceneStateSynchronizer;
    private readonly IEngineRunner _engineRunner;
    private readonly IBuildService _buildService;
    private readonly IDisposable _sessionLease;

    public McpEditorSessionRegistration(
        IMcpEditorSessionAccessor sessionAccessor,
        IActiveProjectService activeProjectService,
        ISceneManagementService sceneManagementService,
        IBehaviourRegistry behaviourRegistry,
        IEntityRenameCommand entityRenameCommand,
        IAssetsService assetsService,
        ISceneStateSynchronizer sceneStateSynchronizer,
        IEngineRunner engineRunner,
        IBuildService buildService)
    {
        _activeProjectService = activeProjectService;
        _sceneManagementService = sceneManagementService;
        _behaviourRegistry = behaviourRegistry;
        _entityRenameCommand = entityRenameCommand;
        _assetsService = assetsService;
        _sceneStateSynchronizer = sceneStateSynchronizer;
        _engineRunner = engineRunner;
        _buildService = buildService;
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
            GetEngineInfo());
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
            throw new ReiMcpOperationException("rename_failed", $"Editor did not apply name '{newName}' to entity {entityId}.");
        }

        return new ReiEntityMutationResult(true, CreateEntitySummary(entity, GetEntityDepth(entity)), "Entity renamed. Save project to persist change.");
    }

    public async Task<ReiProjectSaveResult> SaveProjectAsync()
    {
        if (_engineRunner.IsPlaymodeActive.Value)
        {
            throw new ReiMcpOperationException("save_unavailable", "Project cannot be saved while play mode is active.");
        }

        if (_buildService.BuildInProgress.Value)
        {
            throw new ReiMcpOperationException("save_unavailable", "Project cannot be saved while a build is running.");
        }

        if (_assetsService.SaveInProcess.Value)
        {
            throw new ReiMcpOperationException("save_in_progress", "Another project save is already running.");
        }

        _sceneStateSynchronizer.SynchronizeStateWithEngine();
        await _assetsService.SaveProject();

        return new ReiProjectSaveResult(true, DateTimeOffset.UtcNow, "Project saved.");
    }

    private ReiEngineInfo GetEngineInfo()
    {
        if (_engineRunner.IsEngineStarting.Value) return new ReiEngineInfo("starting", _engineRunner.ActiveMode.ToString());
        if (_engineRunner.IsActive.Value) return new ReiEngineInfo("running", _engineRunner.ActiveMode.ToString());
        return new ReiEngineInfo("stopped", null);
    }

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
            .Select(x => new ReiPropertyDetails(x.Name, x.Type.ToString(), x.SourceType, McpValueConverter.ToContractValue(x.Value)))
            .ToList();

        return new ReiBehaviourDetails(behaviour.Id, GetBehaviourName(behaviour.Id), properties);
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
