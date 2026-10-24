using Xunit.Abstractions;
using static Rei.EngineIntegration.Tests.AssetAssertions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Lifecycle")]
[Trait("Area", "Logging")]
public sealed class LoggingLifecycleTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task NativeBurstReachesEditorInOrderWithoutDelayOrDuplicateSubscriptionsAcrossReloads()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.PrepareProjectAsync(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        var path = Path.Combine(engine.ProjectDirectory, "Project", "Scripts", "ReiApp.cpp");
        var token = Guid.NewGuid().ToString("N");
        var prefix = $"Logging fixture asset token={token}";
        var complete = $"Logging fixture complete token={token}";
        var burst = $$"""
            std::vector<std::thread> writers;
            for (i32 writer = 0; writer < 4; ++writer)
                writers.emplace_back([writer]
                {
                    for (i32 index = 0; index < 500; ++index)
                    {
                        LOG_DEBUG("{{prefix}} writer={} item={}", writer, index)
                    }
                });
            for (auto& writer : writers) writer.join();
            LOG("{{complete}} version=1")
            """;
        await File.WriteAllTextAsync(path, (await File.ReadAllTextAsync(path)).Replace(
            "rei::GetProfiler().Register(DESCRIPTORS);", "rei::GetProfiler().Register(DESCRIPTORS);\n" + burst));
        await engine.StartAsync();
        var captures = new List<string[]>();
        async Task AssertDelivered(int version, int expectedCompletions = 1)
        {
            await WaitForConfigAsync(engine); // Independent native asset read, not an Editor running flag.
            await engine.WaitUntilAsync(async () =>
            {
                var logs = await engine.CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "info", ["limit"] = 500 });
                return logs.GetProperty("entries").EnumerateArray().Any(entry => entry.GetProperty("message").GetString() == complete + $" version={version}");
            }, TimeSpan.FromSeconds(5));
            var info = await engine.CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "info", ["limit"] = 500 });
            Assert.Equal(expectedCompletions, info.GetProperty("entries").EnumerateArray().Count(entry => entry.GetProperty("message").GetString() == complete + $" version={version}"));
            var history = await engine.CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "debug", ["limit"] = 500 });
            await File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, $"logging-history-{captures.Count + 1}.json"), history.GetRawText());
            var messages = history.GetProperty("entries").EnumerateArray()
                .Select(entry => entry.GetProperty("message").GetString()!)
                .Where(message => message.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Assert.InRange(messages.Length, 350, 500);
            Assert.Equal(messages.Length, messages.Distinct().Count());
            captures.Add(messages);
        }

        await AssertDelivered(1);
        var buildLogs = await engine.CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "info", ["limit"] = 500 });
        Assert.Contains("Asset build summary", buildLogs.ToString());
        Assert.Contains("Build Complete", buildLogs.ToString());
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await AssertDelivered(1);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await AssertDelivered(1, expectedCompletions: 2);
        // Rebuild the owned fixture through normal import.
        var beforeReload = (await engine.CallAsync("rei_editor_get_profiling_snapshot", new() { ["source"] = "runtime" })).GetProperty("sessionId").GetString();
        await File.WriteAllTextAsync(path, (await File.ReadAllTextAsync(path)).Replace("version=1", "version=2"));
        await engine.RunOperationAsync("rei_editor_refresh_assets");
        await engine.WaitUntilAsync(async () =>
        {
            var state = await engine.CallAsync("rei_editor_get_state");
            if (state.GetProperty("engine").GetProperty("status").GetString() != "running" ||
                state.GetProperty("automation").GetProperty("isBuilding").GetBoolean() ||
                state.GetProperty("automation").GetProperty("isImporting").GetBoolean()) return false;
            var profile = await engine.CallAsync("rei_editor_get_profiling_snapshot", new() { ["source"] = "runtime" });
            return profile.TryGetProperty("sessionId", out var session) && session.GetString() != beforeReload;
        }, TimeSpan.FromMinutes(2));
        await AssertDelivered(2);
        var stdout = await engine.CloseEditorAsync();

        var native = stdout.Split('\n').Where(line => line.Contains("[DEBUG] " + prefix, StringComparison.Ordinal))
            .Select(line => line[(line.IndexOf("[DEBUG] ", StringComparison.Ordinal) + 8)..].TrimEnd('\r')).ToArray();
        Assert.Equal(4 * 2000, native.Length);
        for (var cycle = 0; cycle < captures.Count; cycle++)
        {
            var expected = native.Skip(cycle * 2000).Take(2000).ToArray();
            Assert.Equal(2000, expected.Distinct().Count());
            Assert.Equal(expected.TakeLast(captures[cycle].Length), captures[cycle]);
        }
        Assert.Equal(4, stdout.Split('\n').Count(line => line.Contains("[INFO] " + complete, StringComparison.Ordinal)));
    }
}
