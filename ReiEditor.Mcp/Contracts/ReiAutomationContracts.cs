using System.Text.Json.Serialization;

namespace ReiEditor.Mcp.Contracts;

public static class ReiOperationStatuses
{
    public const string QUEUED = "queued";
    public const string RUNNING = "running";
    public const string SUCCEEDED = "succeeded";
    public const string FAILED = "failed";
    public const string CANCELED = "canceled";
}

public static class ReiOperationKinds
{
    public const string REFRESH_ASSETS = "refresh_assets";
    public const string BUILD_PROJECT = "build_project";
    public const string START_PLAYMODE = "start_playmode";
    public const string STOP_PLAYMODE = "stop_playmode";
}

public static class ReiBuildConfigurations
{
    public const string DEBUG = "debug";
    public const string EDITOR_DEBUG = "editor_debug";
    public const string RELEASE = "release";
    public const string EDITOR_RELEASE = "editor_release";
}

public sealed record ReiAutomationState(
    bool IsImporting,
    bool IsBuilding,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    ReiOperationInfo? ActiveOperation);

public sealed record ReiOperationInfo(
    string Id,
    string Kind,
    string Status,
    double Progress,
    string Message,
    DateTimeOffset CreatedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateTimeOffset? StartedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateTimeOffset? CompletedAtUtc,
    int LogCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    ReiOperationError? Error);

public sealed record ReiOperationError(
    string Code,
    string Message);

public sealed record ReiBuildOptions(
    string Configuration,
    bool ForceSolutionRebuild,
    bool ForceCleanSolutionBuild,
    bool ForceAssetRebuild,
    bool BuildSolution,
    bool BuildAssets);

public sealed record ReiLogList(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? OperationId,
    int TotalCount,
    bool Truncated,
    IReadOnlyList<ReiLogEntry> Entries);

public sealed record ReiLogEntry(
    DateTimeOffset TimestampUtc,
    string Scope,
    string Level,
    string Message,
    string Details);

public sealed record ReiFrameCapture(
    byte[] PngData,
    int Width,
    int Height,
    DateTimeOffset CapturedAtUtc,
    string EngineMode);
