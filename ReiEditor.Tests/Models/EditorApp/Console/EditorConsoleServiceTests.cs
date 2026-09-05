using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Logging;

namespace ReiEditor.Tests.Models.EditorApp.Console;

/// <summary>Verifies console storage, notifications, snapshots, and clearing semantics.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Console")]
public sealed class EditorConsoleServiceTests
{
    /// <summary>Logging preserves each payload, updates count, and publishes each message in order.</summary>
    [Fact]
    public void LogPreservesPayloadAndPublishesMessage()
    {
        var service = new EditorConsoleService();
        var first = new LogMessage(LogScopeEnum.Editor, LogLevelEnum.Info, DateTime.UtcNow, "", "first details");
        var second = new LogMessage(LogScopeEnum.Engine, LogLevelEnum.Warning, DateTime.UtcNow.AddSeconds(1), "Привет 🌍", "second details");
        var published = new List<LogMessage>();
        service.NewLogEvent += published.Add;

        service.Log(first);
        service.Log(second);

        Assert.Equal(2, service.LogsCount.Value);
        Assert.Equal(new[] { first, second }, service.Logs);
        Assert.Equal(new[] { first, second }, published);
    }

    /// <summary>A retrieved log snapshot remains unchanged when later messages are recorded.</summary>
    [Fact]
    public void LogsReturnsStableSnapshot()
    {
        var service = new EditorConsoleService();
        var first = new LogMessage(LogScopeEnum.Editor, LogLevelEnum.Debug, DateTime.UtcNow, "same", "details");
        var second = new LogMessage(LogScopeEnum.Editor, LogLevelEnum.Debug, DateTime.UtcNow, "same", "details");
        service.Log(first);
        var snapshot = service.Logs;

        service.Log(second);

        Assert.Equal(new[] { first }, snapshot);
        Assert.Equal(new[] { first, second }, service.Logs);
    }

    /// <summary>Clearing stored logs resets count and publishes one clear notification.</summary>
    [Fact]
    public void ClearConsoleClearsStoredLogsAndPublishesNotification()
    {
        var service = new EditorConsoleService();
        var clearedCount = 0;
        service.Log(new LogMessage(LogScopeEnum.Editor, LogLevelEnum.Error, DateTime.UtcNow, "error", "details"));
        service.LogsClearedEvent += () => clearedCount++;

        service.ClearConsole();

        Assert.Empty(service.Logs);
        Assert.Equal(0, service.LogsCount.Value);
        Assert.Equal(1, clearedCount);
    }

    /// <summary>Clearing an empty console does not publish a clear notification.</summary>
    [Fact]
    public void ClearConsoleWhenEmptyDoesNotPublishNotification()
    {
        var service = new EditorConsoleService();
        var clearedCount = 0;
        service.LogsClearedEvent += () => clearedCount++;

        service.ClearConsole();

        Assert.Equal(0, clearedCount);
    }
}
