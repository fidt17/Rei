using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[CollectionDefinition(NAME, DisableParallelization = true)]
public sealed class EngineCollection : ICollectionFixture<SharedEngineFixture>
{
    public const string NAME = "Real engine";
}

/// <summary>Starts lazily, so skipped and lifecycle-only runs do not launch a smoke Editor.</summary>
public sealed class SharedEngineFixture : IAsyncLifetime
{
    private EngineIntegrationHarness? _engine;
    private Exception? _resetFailure;
    private Dictionary<string, string>? _diskBaseline;
    private readonly Dictionary<(string Id, string Source), JsonElement> _baseline = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task RunAsync(string name, ITestOutputHelper output, Func<EngineIntegrationHarness, Task> test)
    {
        if (_resetFailure != null) throw new InvalidOperationException("Shared engine reset failed; refusing contaminated state.", _resetFailure);
        if (_engine == null)
        {
            _engine = new EngineIntegrationHarness();
            output.WriteLine($"Artifacts: {_engine.RunDirectory}");
            try
            {
                await _engine.StartAsync();
                await AssetAssertions.WaitForConfigAsync(_engine);
                foreach (var id in new[] { AssetAssertions.CONFIG_ID, AssetAssertions.ALTERNATE_ID, AssetAssertions.MATERIAL_ID })
                    foreach (var source in new[] { "editor", "runtime" })
                        _baseline[(id, source)] = await _engine.ReadAssetAsync(id, source);
                await _engine.CallAsync("rei_editor_save_project");
                _diskBaseline = SnapshotDisk();
            }
            catch (Exception error)
            {
                _resetFailure = error;
                throw;
            }
        }
        output.WriteLine($"Editor PID: {_engine.ProcessId}; launches: {_engine.LaunchCount}; artifacts: {_engine.RunDirectory}");
        var elapsed = Stopwatch.StartNew();
        Exception? testFailure = null;
        try
        {
            await AssertBaselineAsync();
            await _engine.RunOperationAsync("rei_editor_start_playmode");
            await AssetAssertions.WaitForConfigAsync(_engine);
            await test(_engine);
        }
        catch (Exception error)
        {
            testFailure = error;
            throw;
        }
        finally
        {
            try
            {
                await _engine.RunOperationAsync("rei_editor_stop_playmode");
                await AssetAssertions.WaitForConfigAsync(_engine);
                await AssertBaselineAsync();
                Assert.Equal(1, _engine.LaunchCount);
            }
            catch (Exception error)
            {
                _resetFailure = error;
                if (testFailure != null) throw new AggregateException($"Test and reset both failed. Artifacts: {_engine.RunDirectory}", testFailure, error);
                throw new InvalidOperationException($"Reset after {name} failed. Artifacts: {_engine.RunDirectory}", error);
            }
            finally
            {
                await _engine.RecordTimingAsync(name, elapsed.Elapsed);
            }
        }
    }

    private async Task AssertBaselineAsync()
    {
        foreach (var ((id, source), expected) in _baseline)
        {
            var actual = await _engine!.ReadAssetAsync(id, source);
            Assert.Equal(expected.GetProperty("status").GetString(), actual.GetProperty("status").GetString());
            AssetAssertions.EqualJson(expected.GetProperty("values"), actual.GetProperty("values"));
        }
        Assert.Equal(_diskBaseline!.OrderBy(x => x.Key), SnapshotDisk().OrderBy(x => x.Key));
    }

    private Dictionary<string, string> SnapshotDisk() =>
        Directory.EnumerateFiles(Path.Combine(_engine!.ProjectDirectory, "Project"), "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".asset" or ".mat")
            .ToDictionary(path => Path.GetRelativePath(_engine.ProjectDirectory, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));

    public async Task DisposeAsync()
    {
        if (_engine != null) await _engine.DisposeAsync();
    }
}
