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

public sealed record ReiProjectSaveResult(
    bool Saved,
    DateTimeOffset CompletedAtUtc,
    string Message);
