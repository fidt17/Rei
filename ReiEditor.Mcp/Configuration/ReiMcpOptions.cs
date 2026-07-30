namespace ReiEditor.Mcp.Configuration;

public sealed class ReiMcpOptions
{
    public const int DEFAULT_PORT = 18777;
    public const string MCP_PATH = "/mcp";
    public const string HEALTH_PATH = "/health";

    private const string ENABLED_ENVIRONMENT_VARIABLE = "REI_MCP_ENABLED";
    private const string PORT_ENVIRONMENT_VARIABLE = "REI_MCP_PORT";

    public bool Enabled { get; init; } = true;
    public int Port { get; init; } = DEFAULT_PORT;

    public static ReiMcpOptions FromEnvironment(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        return new ReiMcpOptions
        {
            Enabled = ParseEnabled(getEnvironmentVariable(ENABLED_ENVIRONMENT_VARIABLE)),
            Port = ParsePort(getEnvironmentVariable(PORT_ENVIRONMENT_VARIABLE))
        };
    }

    public void Validate()
    {
        if (Port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(Port), Port, "MCP port must be between 0 and 65535.");
        }
    }

    private static bool ParseEnabled(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (bool.TryParse(value, out var enabled)) return enabled;

        throw new ArgumentException($"{ENABLED_ENVIRONMENT_VARIABLE} must be 'true' or 'false'.", ENABLED_ENVIRONMENT_VARIABLE);
    }

    private static int ParsePort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DEFAULT_PORT;
        if (int.TryParse(value, out var port) && port is >= 1 and <= 65535) return port;

        throw new ArgumentException($"{PORT_ENVIRONMENT_VARIABLE} must be an integer between 1 and 65535.", PORT_ENVIRONMENT_VARIABLE);
    }
}
