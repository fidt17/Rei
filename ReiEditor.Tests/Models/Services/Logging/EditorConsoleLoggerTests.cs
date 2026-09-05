using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Tests.Models.Services.Logging;

/// <summary>Verifies editor logger messages are forwarded to editor console with diagnostic context.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Logging")]
public sealed class EditorConsoleLoggerTests
{
    /// <summary>Marks logger generic context for stack detail assertions.</summary>
    private sealed class TestLogOwner { }

    /// <summary>Each text logging method preserves message and maps to editor scope and expected level.</summary>
    [Theory]
    [InlineData(LogLevelEnum.Info, "info")]
    [InlineData(LogLevelEnum.Warning, "warning")]
    [InlineData(LogLevelEnum.Error, "error")]
    public void TextLogsForwardScopeLevelMessageAndDetails(LogLevelEnum level, string message)
    {
        var console = new EditorConsoleService();
        var logger = new EditorConsoleLogger<TestLogOwner>(console, new SystemConsoleLogger<TestLogOwner>());

        if (level == LogLevelEnum.Info) logger.Log(message);
        else if (level == LogLevelEnum.Warning) logger.LogWarning(message);
        else logger.LogError(message);

        var entry = Assert.Single(console.Logs);
        Assert.Equal(LogScopeEnum.Editor, entry.Scope);
        Assert.Equal(level, entry.Level);
        Assert.Equal(message, entry.Message);
        Assert.Contains(nameof(TestLogOwner), entry.Details);
        Assert.Contains("Stack Trace", entry.Details);
    }

    /// <summary>Exception logging records full exception text as an editor error.</summary>
    [Fact]
    public void ExceptionLogForwardsExceptionTextAsError()
    {
        var console = new EditorConsoleService();
        var logger = new EditorConsoleLogger<TestLogOwner>(console, new SystemConsoleLogger<TestLogOwner>());
        var exception = new InvalidOperationException("controlled failure");

        logger.LogException(exception);

        var entry = Assert.Single(console.Logs);
        Assert.Equal(LogScopeEnum.Editor, entry.Scope);
        Assert.Equal(LogLevelEnum.Error, entry.Level);
        Assert.Equal(exception.ToString(), entry.Message);
        Assert.Contains(nameof(TestLogOwner), entry.Details);
    }

    /// <summary>Empty-line requests do not add editor console entries.</summary>
    [Fact]
    public void AddEmptyLineDoesNotAddConsoleEntry()
    {
        var console = new EditorConsoleService();
        var logger = new EditorConsoleLogger<TestLogOwner>(console, new SystemConsoleLogger<TestLogOwner>());

        logger.AddEmptyLine();

        Assert.Empty(console.Logs);
    }
}
