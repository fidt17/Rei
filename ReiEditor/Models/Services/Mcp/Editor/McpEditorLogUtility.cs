using System;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.Services.Logging;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal static class McpEditorLogUtility
{
    private const int MAX_MESSAGE_LENGTH = 4096;
    private const int MAX_DETAILS_LENGTH = 16384;

    public static ReiLogEntry CreateEntry(LogMessage message)
    {
        return new ReiLogEntry(
            new DateTimeOffset(message.Time.ToUniversalTime()),
            message.Scope.ToString().ToLowerInvariant(),
            message.Level.ToString().ToLowerInvariant(),
            Truncate(message.Message, MAX_MESSAGE_LENGTH),
            Truncate(message.Details, MAX_DETAILS_LENGTH));
    }

    public static int ParseMinimumLevel(string level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            throw new ReiMcpOperationException("invalid_log_level", "Minimum log level must not be empty.");
        }

        var normalizedLevel = level.Trim().ToLowerInvariant();
        var rank = GetLevelRank(normalizedLevel);
        if (rank >= 0) return rank;

        throw new ReiMcpOperationException("invalid_log_level", $"Unknown log level {level}. Expected debug, info, warning, or error.");
    }

    public static int GetLevelRank(string level)
    {
        return level switch
        {
            "debug" => 0,
            "info" => 1,
            "warning" => 2,
            "error" => 3,
            _ => -1
        };
    }

    private static string Truncate(string value, int maximumLength)
    {
        if (value.Length <= maximumLength) return value;
        return value[..maximumLength] + "...";
    }
}
