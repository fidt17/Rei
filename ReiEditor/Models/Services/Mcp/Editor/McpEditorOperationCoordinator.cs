using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal sealed class McpEditorOperationCoordinator : IMcpEditorOperationCoordinator, IDisposable
{
    private sealed class OperationState
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }
        public required CancellationTokenSource CancellationTokenSource { get; init; }
        public string Status { get; set; } = ReiOperationStatuses.QUEUED;
        public double Progress { get; set; }
        public string Message { get; set; } = "Queued.";
        public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public ReiOperationError? Error { get; set; }
        public List<ReiLogEntry> Logs { get; } = [];
    }

    private const int MAX_COMPLETED_OPERATIONS = 20;
    private const int MAX_LOGS_PER_OPERATION = 1000;

    private readonly object _sync = new();
    private readonly Dictionary<string, OperationState> _operations = new(StringComparer.OrdinalIgnoreCase);
    private readonly IEditorConsoleService _consoleService;
    private readonly ILogger<McpEditorOperationCoordinator> _logger;

    private OperationState? _activeOperation;
    private bool _disposed;

    public McpEditorOperationCoordinator(
        IEditorConsoleService consoleService,
        ILogger<McpEditorOperationCoordinator> logger)
    {
        _consoleService = consoleService;
        _logger = logger;
        _consoleService.NewLogEvent += HandleNewLog;
    }

    public void Dispose()
    {
        OperationState? activeOperation;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            activeOperation = _activeOperation;
        }

        _consoleService.NewLogEvent -= HandleNewLog;
        activeOperation?.CancellationTokenSource.Cancel();
    }

    public ReiOperationInfo? GetActiveOperation()
    {
        lock (_sync)
        {
            return _activeOperation == null ? null : CreateInfo(_activeOperation);
        }
    }

    public ReiOperationInfo Start(string kind, Func<McpEditorOperationContext, Task<string>> operation)
    {
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Operation kind is required.", nameof(kind));
        ArgumentNullException.ThrowIfNull(operation);

        OperationState state;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_activeOperation != null)
            {
                throw new ReiMcpOperationException(
                    "operation_in_progress",
                    $"Operation {_activeOperation.Id} ({_activeOperation.Kind}) is already {_activeOperation.Status}.");
            }

            CleanupCompletedOperations();
            state = new OperationState
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = kind,
                CancellationTokenSource = new CancellationTokenSource()
            };
            _operations.Add(state.Id, state);
            _activeOperation = state;
        }

        _ = Task.Run(() => RunOperationAsync(state, operation));
        return CreateInfoThreadSafe(state);
    }

    public ReiOperationInfo Get(string operationId)
    {
        var state = GetRequiredOperation(operationId);
        return CreateInfoThreadSafe(state);
    }

    public ReiOperationInfo Cancel(string operationId)
    {
        var state = GetRequiredOperation(operationId);
        ReiOperationInfo operationInfo;
        lock (_sync)
        {
            if (IsTerminal(state.Status)) return CreateInfo(state);
            state.Message = "Cancellation requested.";
            operationInfo = CreateInfo(state);
        }

        state.CancellationTokenSource.Cancel();
        return operationInfo;
    }

    public IReadOnlyList<ReiLogEntry> GetLogs(string operationId)
    {
        var state = GetRequiredOperation(operationId);
        lock (_sync)
        {
            return state.Logs.ToList();
        }
    }

    internal void Report(string operationId, double progress, string message)
    {
        var state = GetRequiredOperation(operationId);
        lock (_sync)
        {
            if (state.Status != ReiOperationStatuses.RUNNING) return;
            state.Progress = Math.Clamp(progress, 0, 1);
            state.Message = string.IsNullOrWhiteSpace(message) ? state.Message : message.Trim();
        }
    }

    internal bool HasErrors(string operationId)
    {
        var state = GetRequiredOperation(operationId);
        lock (_sync)
        {
            return state.Logs.Any(x => string.Equals(x.Level, "error", StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task RunOperationAsync(OperationState state, Func<McpEditorOperationContext, Task<string>> operation)
    {
        try
        {
            state.CancellationTokenSource.Token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                state.Status = ReiOperationStatuses.RUNNING;
                state.StartedAtUtc = DateTimeOffset.UtcNow;
                state.Message = "Running.";
            }
            var context = new McpEditorOperationContext(this, state.Id, state.CancellationTokenSource.Token);
            var message = await operation(context);
            state.CancellationTokenSource.Token.ThrowIfCancellationRequested();

            Complete(
                state,
                ReiOperationStatuses.SUCCEEDED,
                string.IsNullOrWhiteSpace(message) ? "Operation completed." : message.Trim(),
                null);
        }
        catch (OperationCanceledException)
        {
            Complete(state, ReiOperationStatuses.CANCELED, "Operation canceled.", null);
        }
        catch (ReiMcpOperationException exception)
        {
            Complete(
                state,
                ReiOperationStatuses.FAILED,
                exception.Message,
                new ReiOperationError(exception.Code, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
            Complete(
                state,
                ReiOperationStatuses.FAILED,
                "Operation failed. Inspect operation logs.",
                new ReiOperationError("operation_failed", "Operation failed. Inspect operation logs."));
        }
    }

    private void Complete(OperationState state, string status, string message, ReiOperationError? error)
    {
        lock (_sync)
        {
            state.Status = status;
            state.Progress = status == ReiOperationStatuses.SUCCEEDED ? 1 : state.Progress;
            state.Message = message;
            state.Error = error;
            state.CompletedAtUtc = DateTimeOffset.UtcNow;
            if (ReferenceEquals(_activeOperation, state)) _activeOperation = null;
        }
    }

    private OperationState GetRequiredOperation(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw new ReiMcpOperationException("invalid_operation_id", "Operation id must not be empty.");
        }

        lock (_sync)
        {
            if (_operations.TryGetValue(operationId.Trim(), out var state)) return state;
        }

        throw new ReiMcpOperationException("operation_not_found", $"Operation {operationId.Trim()} does not exist in current editor session.");
    }

    private ReiOperationInfo CreateInfoThreadSafe(OperationState state)
    {
        lock (_sync)
        {
            return CreateInfo(state);
        }
    }

    private static ReiOperationInfo CreateInfo(OperationState state)
    {
        return new ReiOperationInfo(
            state.Id,
            state.Kind,
            state.Status,
            state.Progress,
            state.Message,
            state.CreatedAtUtc,
            state.StartedAtUtc,
            state.CompletedAtUtc,
            state.Logs.Count,
            state.Error);
    }

    private void HandleNewLog(LogMessage message)
    {
        lock (_sync)
        {
            if (_activeOperation == null) return;
            if (_activeOperation.Logs.Count >= MAX_LOGS_PER_OPERATION) return;

            _activeOperation.Logs.Add(McpEditorLogUtility.CreateEntry(message));
        }
    }

    private void CleanupCompletedOperations()
    {
        var completed = _operations.Values
            .Where(x => IsTerminal(x.Status))
            .OrderByDescending(x => x.CompletedAtUtc)
            .Skip(MAX_COMPLETED_OPERATIONS - 1)
            .ToList();

        foreach (var state in completed)
        {
            _operations.Remove(state.Id);
            state.CancellationTokenSource.Dispose();
        }
    }

    private static bool IsTerminal(string status)
    {
        return status is ReiOperationStatuses.SUCCEEDED or ReiOperationStatuses.FAILED or ReiOperationStatuses.CANCELED;
    }
}
