using System.Text.Json.Serialization;

namespace ReiEditor.Mcp.Contracts;

public static class ReiEditorStatus
{
    public const string PROJECT_MANAGEMENT = "project_management";
    public const string PROJECT_LOADING = "project_loading";
    public const string READY = "ready";
}

public sealed record ReiEditorState(
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    ReiProjectInfo? Project,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    ReiSceneInfo? Scene,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    ReiEngineInfo? Engine,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    ReiAutomationState? Automation);

public sealed record ReiProjectInfo(
    string Name,
    string RootPath,
    string ProjectFilePath,
    string SolutionPath);

public sealed record ReiSceneInfo(
    string Id,
    string Name,
    int EntityCount);

public sealed record ReiEngineInfo(
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Mode);

public sealed record ReiEntityList(
    string SceneId,
    string SceneName,
    IReadOnlyList<ReiEntitySummary> Entities);

public sealed record ReiEntitySummary(
    int Id,
    string Name,
    int ParentId,
    int Order,
    int Depth,
    IReadOnlyList<ReiBehaviourSummary> Behaviours);

public sealed record ReiEntityDetails(
    int Id,
    string Name,
    int ParentId,
    int Order,
    IReadOnlyList<ReiBehaviourDetails> Behaviours);

public sealed record ReiBehaviourSummary(
    int Id,
    string Name);

public sealed record ReiBehaviourDetails(
    int Id,
    string Name,
    IReadOnlyList<ReiPropertyDetails> Properties);

public sealed record ReiPropertyDetails(
    string Name,
    string Type,
    string SourceType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    object? Value);

public sealed record ReiEntityMutationResult(
    bool Changed,
    ReiEntitySummary Entity,
    string Message);

public sealed record ReiBehaviourMutationResult(
    bool Changed,
    ReiEntityDetails Entity,
    string Message);

public sealed record ReiBehaviourPropertyMutationResult(
    bool Changed,
    int EntityId,
    ReiBehaviourDetails Behaviour,
    ReiPropertyDetails Property,
    string Message);

public sealed record ReiMaterialPropertyMutationResult(
    bool Changed,
    string MaterialAssetId,
    string ShaderAssetId,
    ReiPropertyDetails Property,
    bool RuntimeSynced,
    string Message);

public sealed record ReiDataAssetTypeList(
    IReadOnlyList<ReiDataAssetTypeDetails> Types);

public sealed record ReiDataAssetTypeDetails(
    int TypeId,
    string Name,
    string Namespace,
    IReadOnlyList<ReiPropertySchema> Properties);

public sealed record ReiPropertySchema(
    string Name,
    string Type,
    string SourceType);

public sealed record ReiDataAssetList(
    IReadOnlyList<ReiDataAssetSummary> Assets);

public sealed record ReiDataAssetSummary(
    string AssetId,
    int TypeId,
    string TypeName,
    string ProjectPath);

public sealed record ReiDataAssetDetails(
    string AssetId,
    int TypeId,
    string TypeName,
    string ProjectPath,
    IReadOnlyList<ReiPropertyDetails> Properties);

public sealed record ReiDataAssetCreationResult(
    bool Created,
    ReiDataAssetDetails Asset,
    string Message);

public sealed record ReiDataAssetPropertyMutationResult(
    bool Changed,
    ReiDataAssetDetails Asset,
    ReiPropertyDetails Property,
    bool RuntimeSynced,
    string Message);

public sealed record ReiProjectSaveResult(
    bool Saved,
    DateTimeOffset CompletedAtUtc,
    string Message);
