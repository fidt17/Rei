using System.Collections.Concurrent;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

public sealed record TestLogEntry(LogLevelEnum Level, string Message, Exception? Exception = null);

public sealed class TestLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<TestLogEntry> _entries = new();

    public IReadOnlyList<TestLogEntry> Entries => _entries.ToArray();

    public void AddEmptyLine() => Log("");
    public void Log(string message) => _entries.Enqueue(new TestLogEntry(LogLevelEnum.Info, message));
    public void LogWarning(string message) => _entries.Enqueue(new TestLogEntry(LogLevelEnum.Warning, message));
    public void LogError(string message) => _entries.Enqueue(new TestLogEntry(LogLevelEnum.Error, message));
    public void LogException(Exception exception) => _entries.Enqueue(new TestLogEntry(LogLevelEnum.Error, exception.Message, exception));
}
