using ReiEditor.Mcp.Configuration;

namespace ReiEditor.Mcp.Tests.Configuration;

public sealed class ReiMcpOptionsTests
{
    [Fact]
    public void FromEnvironmentUsesSafeDefaults()
    {
        var options = ReiMcpOptions.FromEnvironment(_ => null);

        Assert.True(options.Enabled);
        Assert.Equal(ReiMcpOptions.DEFAULT_PORT, options.Port);
    }

    [Fact]
    public void FromEnvironmentReadsEnabledAndPort()
    {
        var values = new Dictionary<string, string?>
        {
            ["REI_MCP_ENABLED"] = "false",
            ["REI_MCP_PORT"] = "19001"
        };

        var options = ReiMcpOptions.FromEnvironment(name => values.GetValueOrDefault(name));

        Assert.False(options.Enabled);
        Assert.Equal(19001, options.Port);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void FromEnvironmentRejectsInvalidPort(string value)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ReiMcpOptions.FromEnvironment(name => name == "REI_MCP_PORT" ? value : null));

        Assert.Contains("REI_MCP_PORT", exception.Message);
    }
}
