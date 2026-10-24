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

    [Fact]
    public async Task ConcurrentWritersPublishInStorageOrderAndClearCannotReviveEarlierMessages()
    {
        var service = new EditorConsoleService();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var published = new List<string>();
        var counts = new List<int>();
        service.LogsCount.Subscribe(counts.Add, invoke: false);
        service.NewLogEvent += message =>
        {
            if (message.Message == "first")
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            }
            published.Add(message.Message);
        };
        service.LogsClearedEvent += () => published.Add("cleared");
        LogMessage Message(string text) => new(LogScopeEnum.Engine, LogLevelEnum.Debug, DateTime.UtcNow, text, "");
        var first = Task.Run(() => service.Log(Message("first")));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            await Task.Run(() => service.Log(Message("second"))).WaitAsync(TimeSpan.FromSeconds(2));
            service.ClearConsole();
            service.Log(Message("current"));
            Assert.Equal("current", Assert.Single(service.Logs).Message);
        }
        finally
        {
            release.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(new[] { "first", "second", "cleared", "current" }, published);
        Assert.Equal(new[] { 1, 2, 0, 1 }, counts);
        Assert.Equal(1, service.LogsCount.Value);
    }

    [Fact]
    public void SubscriberCanLogAndClearReentrantlyWithoutChangingNotificationOrder()
    {
        var service = new EditorConsoleService();
        var published = new List<string>();
        service.NewLogEvent += message =>
        {
            published.Add(message.Message);
            if (message.Message != "outer") return;
            service.ClearConsole();
            service.Log(new(LogScopeEnum.Editor, LogLevelEnum.Info, DateTime.UtcNow, "inner", ""));
        };
        service.LogsClearedEvent += () => published.Add("cleared");
        service.Log(new(LogScopeEnum.Editor, LogLevelEnum.Info, DateTime.UtcNow, "outer", ""));
        Assert.Equal(new[] { "outer", "cleared", "inner" }, published);
        Assert.Equal("inner", Assert.Single(service.Logs).Message);
    }
}
