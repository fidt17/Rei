using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Rei.EngineIntegration.Tests;

public sealed class EngineIntegrationHarness : IAsyncDisposable
{
    private readonly string _fixtureName;
    private readonly bool _keepBuildOutputs;
    private bool _prepared;
    private readonly SemaphoreSlim _diagnosticWriteLock = new(1, 1);
    public int LaunchCount { get; private set; }
    public int ProcessId => _editor?.Id ?? throw new InvalidOperationException("Editor is not running.");
    private Process? _editor;

    private McpClient? _client;
    private Task? _stdout;
    private Task? _stderr;
    public string RunDirectory { get; } = Path.Combine(Path.GetTempPath(), "Rei-engine-tests", Guid.NewGuid().ToString("N"));
    public string ProjectDirectory => Path.Combine(RunDirectory, "project");

    public EngineIntegrationHarness(string fixtureName = "DataAssets", bool? keepBuildOutputs = null)
    {
        if (string.IsNullOrWhiteSpace(fixtureName) || Path.GetFileName(fixtureName) != fixtureName || fixtureName is "." or "..")
            throw new ArgumentException("Fixture name must be a single directory name.", nameof(fixtureName));
        _fixtureName = fixtureName;
        _keepBuildOutputs = keepBuildOutputs ?? string.Equals(Environment.GetEnvironmentVariable("REI_TEST_KEEP_BUILD_OUTPUTS"), "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task StartAsync()
    {
        if (_editor != null) throw new InvalidOperationException("Editor already started. Use RestartAsync.");
        var startup = Stopwatch.StartNew();
        Directory.CreateDirectory(RunDirectory);
        var executable = RequireFile("REI_TEST_EDITOR_EXE");
        var engineFile = RequireFile("REI_TEST_ENGINE_FILE");
        var msbuild = RequireFile("REI_TEST_MSBUILD");
        var storage = Path.Combine(RunDirectory, "storage");
        if (!_prepared)
        {
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", _fixtureName);
            CopyTree(fixture, ProjectDirectory);
            var projectFile = Directory.GetFiles(ProjectDirectory, "*.rei").Single();
            var project = JsonNode.Parse(await File.ReadAllTextAsync(projectFile))!;
            project["ProjectSolutionPath"] = ResolveFixturePath(project["ProjectSolutionPath"]!.GetValue<string>());
            project["ProjectVisualStudioProjectPath"] = ResolveFixturePath(project["ProjectVisualStudioProjectPath"]!.GetValue<string>());
            await File.WriteAllTextAsync(projectFile, project.ToJsonString());
            var vcxproj = project["ProjectVisualStudioProjectPath"]!.GetValue<string>();
            await File.WriteAllTextAsync(vcxproj, (await File.ReadAllTextAsync(vcxproj)).Replace("__REI_ROOT__", Path.GetDirectoryName(engineFile)!));
            Directory.CreateDirectory(storage);
            await File.WriteAllTextAsync(Path.Combine(storage, "preferences.json"), JsonSerializer.Serialize(new
            {
                EnginePath = engineFile, MsBuildPath = msbuild, BookmarkedProjectsPaths = Array.Empty<string>()
            }));
            _prepared = true;
        }
        var startupProject = Directory.GetFiles(ProjectDirectory, "*.rei").Single();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = RunDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        start.Environment["REI_EDITOR_STORAGE"] = storage;
        start.Environment["REI_STARTUP_PROJECT"] = startupProject;
        start.Environment["REI_MCP_ENABLED"] = "true";
        start.Environment["REI_MCP_PORT"] = port.ToString();
        _editor = Process.Start(start) ?? throw new InvalidOperationException("Editor failed to start.");
        LaunchCount++;
        _stdout = CaptureOutputAsync(_editor.StandardOutput, Path.Combine(RunDirectory, $"stdout-{LaunchCount}.log"));
        _stderr = CaptureOutputAsync(_editor.StandardError, Path.Combine(RunDirectory, $"stderr-{LaunchCount}.log"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var endpoint = new Uri($"http://127.0.0.1:{port}/mcp");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        await WaitForHealthAsync(http, new Uri(endpoint, "/health"), EnsureAlive, timeout.Token);
        _client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint, TransportMode = HttpTransportMode.StreamableHttp
        }), cancellationToken: timeout.Token);
        await WaitUntilAsync(async () =>
        {
            var state = await CallAsync("rei_editor_get_state");
            if (state.TryGetProperty("automation", out var automation) && automation.ValueKind == JsonValueKind.Object &&
                !automation.GetProperty("isBuilding").GetBoolean())
            {
                var logs = await CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "error", ["limit"] = 20 });
                if (logs.ToString().Contains("Build Failed") || logs.ToString().Contains("Build errors:"))
                    throw new InvalidOperationException($"Startup build failed. Artifacts: {RunDirectory}");
            }
            return state.GetProperty("status").GetString() == "ready" &&
                state.GetProperty("engine").GetProperty("status").GetString() == "running" &&
                !state.GetProperty("automation").GetProperty("isBuilding").GetBoolean() &&
                !state.GetProperty("automation").GetProperty("isImporting").GetBoolean();
        }, TimeSpan.FromMinutes(4));
        var ready = await CallAsync("rei_editor_get_state");
        Assert.Equal(Path.GetFullPath(ProjectDirectory), Path.GetFullPath(ready.GetProperty("project").GetProperty("rootPath").GetString()!));
        var errors = await CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "error", ["limit"] = 500 });
        Assert.True(errors.GetProperty("entries").GetArrayLength() == 0, $"Startup errors: {errors}. Artifacts: {RunDirectory}");
        await RecordTimingAsync("startup", startup.Elapsed);
    }

    internal static async Task WaitForHealthAsync(HttpClient http, Uri endpoint, Action ensureAlive, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ensureAlive();
            try
            {
                using var response = await http.GetAsync(endpoint, cancellationToken);
                if (response.IsSuccessStatusCode) break;
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(200, cancellationToken);
        }
    }

    public async Task<JsonElement> CallAsync(string tool, Dictionary<string, object?>? arguments = null)
    {
        EnsureAlive();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await _client!.CallToolAsync(tool, arguments, cancellationToken: timeout.Token);
        // Preserve native framebuffer pixels alongside metadata for visual regression checks.
        foreach (var image in result.Content.OfType<ImageContentBlock>().Where(image => image.MimeType == "image/png"))
            await File.WriteAllBytesAsync(Path.Combine(RunDirectory, $"frame-{Guid.NewGuid():N}.png"), image.DecodedData.ToArray());
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
        await AppendDiagnosticAsync("mcp.jsonl",
            JsonSerializer.Serialize(new { tool, arguments, result = text, isError = result.IsError }));
        if (result.IsError == true) throw new InvalidOperationException($"{tool}: {text}. Artifacts: {RunDirectory}");
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    public async Task AssertToolErrorAsync(string tool, Dictionary<string, object?> arguments, string code)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await _client!.CallToolAsync(tool, arguments, cancellationToken: timeout.Token);
        Assert.True(result.IsError);
        Assert.Contains(code, string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text)));
    }

    public Task<JsonElement> ReadAssetAsync(string id, string source) =>
        CallAsync("rei_editor_get_asset_state", new() { ["assetId"] = id, ["source"] = source });

    public async Task RunOperationAsync(string tool, Dictionary<string, object?>? arguments = null)
    {
        var elapsed = Stopwatch.StartNew();
        var started = await CallAsync(tool, arguments);
        var id = started.GetProperty("id").GetString();
        await WaitUntilAsync(async () =>
        {
            var operation = await CallAsync("rei_editor_get_operation", new() { ["operationId"] = id });
            var status = operation.GetProperty("status").GetString();
            if (status is "failed" or "canceled") throw new InvalidOperationException(operation.ToString());
            return status == "succeeded";
        }, TimeSpan.FromMinutes(4));
        await RecordTimingAsync(tool, elapsed.Elapsed);
    }

    public async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan? timeout = null)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < (timeout ?? TimeSpan.FromSeconds(10)))
        {
            EnsureAlive();
            if (await condition()) return;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Engine condition timed out. Artifacts: {RunDirectory}");
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    public Task RecordTimingAsync(string phase, TimeSpan elapsed) =>
        AppendDiagnosticAsync("timings.jsonl",
            JsonSerializer.Serialize(new { phase, milliseconds = elapsed.TotalMilliseconds, launch = LaunchCount, processId = ProcessId }));

    internal async Task AppendDiagnosticAsync(string fileName, string json)
    {
        await _diagnosticWriteLock.WaitAsync();
        try { await File.AppendAllTextAsync(Path.Combine(RunDirectory, fileName), json + Environment.NewLine); }
        finally { _diagnosticWriteLock.Release(); }
    }

    // Uses only this harness's isolated build/resources and owns process teardown.
    public async Task<string> RunStandaloneSmokeAsync(string relativeExecutable)
    {
        var executable = ResolveFixturePath(relativeExecutable);
        if (!File.Exists(executable)) throw new FileNotFoundException("Build the isolated standalone project first.", executable);
        await StopAsync();
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.Combine(ProjectDirectory, "bin"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Standalone failed to start.");
        var stdoutPath = Path.Combine(RunDirectory, "standalone-stdout.log");
        var stdout = CaptureOutputAsync(process.StandardOutput, stdoutPath);
        var stderr = CaptureOutputAsync(process.StandardError, Path.Combine(RunDirectory, "standalone-stderr.log"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (process.MainWindowHandle == IntPtr.Zero)
            {
                if (process.HasExited) throw new InvalidOperationException($"Standalone exited before window creation: {process.ExitCode}. Artifacts: {RunDirectory}");
                await Task.Delay(50, timeout.Token);
                process.Refresh();
            }
            await Task.Delay(1000, timeout.Token);
            if (!process.CloseMainWindow()) throw new InvalidOperationException("Standalone window refused close request.");
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(1, process.ExitCode); // MAIN_WINDOW_CLOSED_EXIT_CODE
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
        }
        var logs = await File.ReadAllTextAsync(stdoutPath);
        Assert.Contains("Engine update loop started", logs);
        Assert.Contains("Shutdown complete", logs);
        Assert.DoesNotContain("[ERROR]", logs);
        return logs;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_keepBuildOutputs) return;
        try { TrimBuildOutputs(RunDirectory); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Preserve evidence and report cleanup failure without hiding the original test failure.
            await File.WriteAllTextAsync(Path.Combine(RunDirectory, "cleanup-error.txt"), error.ToString());
        }
    }

    internal static void TrimBuildOutputs(string runDirectory)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Rei-engine-tests")).TrimEnd(Path.DirectorySeparatorChar);
        var run = Path.GetFullPath(runDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!Guid.TryParseExact(Path.GetFileName(run), "N", out _) ||
            !string.Equals(Path.GetDirectoryName(run), root, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Cleanup only accepts immediate GUID directories under Rei-engine-tests.");
        if (!Directory.Exists(run)) return;
        var outputs = Path.GetFullPath(Path.Combine(run, "project", "bin"));
        foreach (var parent in new[] { root, run, Path.Combine(run, "project") })
            if (Directory.Exists(parent)) RejectLink(parent);
        if (!Directory.Exists(outputs)) return;
        ValidateTree(outputs);
        Directory.Delete(outputs, recursive: true);
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Refusing cleanup through a link: {path}");
    }

    private static void ValidateTree(string directory)
    {
        RejectLink(directory);
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            RejectLink(entry);
            if (Directory.Exists(entry)) ValidateTree(entry);
        }
    }

    private async Task StopAsync()
    {
        try
        {
            if (_client != null && _editor is { HasExited: false })
            {
                try { await CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "info", ["limit"] = 500 }); }
                catch (Exception error) { await File.WriteAllTextAsync(Path.Combine(RunDirectory, "diagnostics-error.txt"), error.ToString()); }
            }
            if (_client != null) await _client.DisposeAsync();
        }
        finally
        {
            if (_editor != null)
            {
                // Only the process started by this harness and its children are terminated.
                if (!_editor.HasExited) _editor.Kill(entireProcessTree: true);
                await _editor.WaitForExitAsync();
                if (_stdout != null) await _stdout;
                if (_stderr != null) await _stderr;
                _editor.Dispose();
                _editor = null;
                _client = null;
                _stdout = null;
                _stderr = null;
            }
        }
    }

    private string ResolveFixturePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(ProjectDirectory, relativePath));
        if (Path.IsPathFullyQualified(relativePath) || !fullPath.StartsWith(ProjectDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Fixture project paths must stay inside the isolated project.");
        return fullPath;
    }

    private void EnsureAlive()
    {
        if (_editor is { HasExited: true })
            throw new InvalidOperationException($"Editor exited {_editor.ExitCode}. Artifacts: {RunDirectory}");
    }

    private static string RequireFile(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value) || !File.Exists(value))
            throw new InvalidOperationException($"Set {variable} to an existing file.");
        return Path.GetFullPath(value);
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var directory in Directory.GetDirectories(source))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Fixture must not contain directory links.");
            CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static async Task CaptureOutputAsync(StreamReader reader, string path)
    {
        await using var writer = new StreamWriter(path) { AutoFlush = true };
        while (await reader.ReadLineAsync() is { } line) await writer.WriteLineAsync(line);
    }
}
