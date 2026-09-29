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
    private Process? _editor;

    private McpClient? _client;
    private Task? _stdout;
    private Task? _stderr;
    public string RunDirectory { get; } = Path.Combine(Path.GetTempPath(), "Rei-engine-tests", Guid.NewGuid().ToString("N"));
    public string ProjectDirectory => Path.Combine(RunDirectory, "project");

    public EngineIntegrationHarness(string fixtureName = "DataAssets")
    {
        if (string.IsNullOrWhiteSpace(fixtureName) || Path.GetFileName(fixtureName) != fixtureName || fixtureName is "." or "..")
            throw new ArgumentException("Fixture name must be a single directory name.", nameof(fixtureName));
        _fixtureName = fixtureName;
    }

    public async Task StartAsync()
    {
        Directory.CreateDirectory(RunDirectory);
        var executable = RequireFile("REI_TEST_EDITOR_EXE");
        var engineFile = RequireFile("REI_TEST_ENGINE_FILE");
        var msbuild = RequireFile("REI_TEST_MSBUILD");
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", _fixtureName);
        CopyTree(fixture, ProjectDirectory);
        var projectFile = Directory.GetFiles(ProjectDirectory, "*.rei").Single();
        var project = JsonNode.Parse(await File.ReadAllTextAsync(projectFile))!;
        project["ProjectSolutionPath"] = ResolveFixturePath(project["ProjectSolutionPath"]!.GetValue<string>());
        project["ProjectVisualStudioProjectPath"] = ResolveFixturePath(project["ProjectVisualStudioProjectPath"]!.GetValue<string>());
        await File.WriteAllTextAsync(projectFile, project.ToJsonString());
        var vcxproj = project["ProjectVisualStudioProjectPath"]!.GetValue<string>();
        await File.WriteAllTextAsync(vcxproj, (await File.ReadAllTextAsync(vcxproj)).Replace("__REI_ROOT__", Path.GetDirectoryName(engineFile)!));
        var storage = Path.Combine(RunDirectory, "storage");
        Directory.CreateDirectory(storage);
        await File.WriteAllTextAsync(Path.Combine(storage, "preferences.json"), JsonSerializer.Serialize(new
        {
            EnginePath = engineFile, MsBuildPath = msbuild, BookmarkedProjectsPaths = Array.Empty<string>()
        }));
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
        start.Environment["REI_STARTUP_PROJECT"] = projectFile;
        start.Environment["REI_MCP_ENABLED"] = "true";
        start.Environment["REI_MCP_PORT"] = port.ToString();
        _editor = Process.Start(start) ?? throw new InvalidOperationException("Editor failed to start.");
        _stdout = CaptureOutputAsync(_editor.StandardOutput, Path.Combine(RunDirectory, "stdout.log"));
        _stderr = CaptureOutputAsync(_editor.StandardError, Path.Combine(RunDirectory, "stderr.log"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var endpoint = new Uri($"http://127.0.0.1:{port}/mcp");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        while (true)
        {
            EnsureAlive();
            try
            {
                using var response = await http.GetAsync(new Uri(endpoint, "/health"), timeout.Token);
                if (response.IsSuccessStatusCode) break;
            }
            catch (HttpRequestException) { }
            await Task.Delay(200, timeout.Token);
        }
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
    }

    public async Task<JsonElement> CallAsync(string tool, Dictionary<string, object?>? arguments = null)
    {
        EnsureAlive();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await _client!.CallToolAsync(tool, arguments, cancellationToken: timeout.Token);
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
        await File.AppendAllTextAsync(Path.Combine(RunDirectory, "mcp.jsonl"),
            JsonSerializer.Serialize(new { tool, arguments, result = text, isError = result.IsError }) + Environment.NewLine);
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
        var started = await CallAsync(tool, arguments);
        var id = started.GetProperty("id").GetString();
        await WaitUntilAsync(async () =>
        {
            var operation = await CallAsync("rei_editor_get_operation", new() { ["operationId"] = id });
            var status = operation.GetProperty("status").GetString();
            if (status is "failed" or "canceled") throw new InvalidOperationException(operation.ToString());
            return status == "succeeded";
        }, TimeSpan.FromMinutes(4));
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

    public async ValueTask DisposeAsync()
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
