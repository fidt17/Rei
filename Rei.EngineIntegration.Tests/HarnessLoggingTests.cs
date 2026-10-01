using System.Text.Json;

namespace Rei.EngineIntegration.Tests;

public sealed class HarnessLoggingTests
{
    [Fact]
    public async Task ConcurrentDiagnosticWritesPreserveEveryJsonLine()
    {
        await using var harness = new EngineIntegrationHarness();
        Directory.CreateDirectory(harness.RunDirectory);
        await Task.WhenAll(Enumerable.Range(0, 64).Select(id =>
            harness.AppendDiagnosticAsync("mcp.jsonl", JsonSerializer.Serialize(new { id }))));

        var lines = await File.ReadAllLinesAsync(Path.Combine(harness.RunDirectory, "mcp.jsonl"));
        Assert.Equal(64, lines.Length);
        var ids = lines.Select(line =>
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.GetProperty("id").GetInt32();
        }).OrderBy(id => id).ToArray();
        Assert.Equal(Enumerable.Range(0, 64), ids);
    }
}
