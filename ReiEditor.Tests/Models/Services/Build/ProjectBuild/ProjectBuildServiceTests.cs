using System.Globalization;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Build.ProjectBuild;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Tests.Models.Services.Build.ProjectBuild;

/// <summary>Verifies standalone executable orchestration and packaging with isolated files and recording fakes.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class ProjectBuildServiceTests
{
    private sealed record TestBuildCall(
        BuildConfigurationEnum Configuration,
        bool ForceSolution,
        bool CleanSolution,
        bool ForceAssets,
        bool BuildSolution,
        bool BuildAssets,
        Action<AssetBuildProgressInfo>? Progress);

    private sealed class TestBuildStarter(List<string> calls) : IBuildStarter
    {
        public ICondition CanStartBuild => throw new NotSupportedException();
        public List<TestBuildCall> Requests { get; } = new();
        public Func<TestBuildCall, Task<bool>> OnBuild { get; set; } = _ => Task.FromResult(true);

        public Task<bool> BuildProject(BuildConfigurationEnum configurationEnum, bool forceSolutionRebuild = false, bool forceCleanSolutionBuild = false, bool forceAssetRebuild = false, BuildExecutionContext? buildContext = null, bool buildSolution = true, bool buildAssets = true, Action<AssetBuildProgressInfo>? onAssetBuilding = null, CancellationToken cancellationToken = default)
        {
            Assert.Null(buildContext);
            cancellationToken.ThrowIfCancellationRequested();
            var request = new TestBuildCall(configurationEnum, forceSolutionRebuild, forceCleanSolutionBuild, forceAssetRebuild, buildSolution, buildAssets, onAssetBuilding);
            Requests.Add(request);
            calls.Add(buildSolution ? "solution" : "assets");
            return OnBuild(request);
        }
    }

    private sealed class TestEditorModeStarter(List<string> calls) : IEditorModeStarter
    {
        public ICondition CanStart => throw new NotSupportedException();
        public int StartCalls { get; private set; }
        public void Start() { StartCalls++; calls.Add("editor"); }
    }

    private sealed class TestFileExplorer(List<string> calls) : IFileExplorerProvider
    {
        public List<string> OpenedDirectories { get; } = new();
        public void OpenDirectory(string directoryPath) { OpenedDirectories.Add(directoryPath); calls.Add("explorer"); }
        public void OpenAndSelect(string path) => throw new NotSupportedException();
    }

    private sealed class TestEngineSettingsProvider(string includeDirectory) : IEngineSettingsProvider
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public string GetEnginePath() => throw new NotSupportedException();
        public string GetEngineDebugIncludeDir() => includeDirectory;
        public string GetEngineReleaseIncludeDir() => includeDirectory;
        public string GetEngineSourceIncludes() => throw new NotSupportedException();
        public string GetEngineResourcesDir() => throw new NotSupportedException();
        public string GetEngineBehavioursDir() => throw new NotSupportedException();
        public string GetEngineVersion() => "test";
    }

    private sealed class TestContext : IDisposable
    {
        public TemporaryProjectFixture Fixture { get; } = new();
        public List<string> Calls { get; } = new();
        public TestBuildStarter Build { get; }
        public TestEditorModeStarter Editor { get; }
        public TestFileExplorer Explorer { get; }
        public TestLogger<ProjectBuildService> Logger { get; } = new();
        public ProjectBuildOutputPathUtility Paths { get; }
        public ProjectBuildService Service { get; }
        public string EngineIncludePath { get; }
        public string ProjectFilePath { get; }
        public string OutputPath => Fixture.Directory.GetPath("package");

        public TestContext()
        {
            Fixture.Project.SetProjectName("Game");
            ProjectFilePath = Fixture.Resources.GetScriptsPath("Game.vcxproj");
            Fixture.Project.SetProjectVisualStudioProjectPath(ProjectFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectFilePath)!);
            File.WriteAllText(ProjectFilePath, """
                <Project>
                  <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
                    <Link><SubSystem>Windows</SubSystem><EntryPointSymbol>mainCRTStartup</EntryPointSymbol></Link>
                  </ItemDefinitionGroup>
                  <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'">
                    <Link><SubSystem>Windows</SubSystem><EntryPointSymbol>mainCRTStartup</EntryPointSymbol></Link>
                  </ItemDefinitionGroup>
                </Project>
                """);

            EngineIncludePath = Fixture.Directory.GetPath("engine");
            Directory.CreateDirectory(EngineIncludePath);
            foreach (var name in new[] { "Rei.lib", "Rei.dll", "assimp-vc143-mt.dll" }) File.WriteAllBytes(Path.Combine(EngineIncludePath, name), new byte[] { 1 });

            var active = new ActiveProjectService(new TestLogger<ActiveProjectService>());
            active.OpenProject(Fixture.Project);
            Build = new(Calls);
            Editor = new(Calls);
            Explorer = new(Calls);
            Paths = new(active, Fixture.Resources);
            Service = new(
                Build,
                Editor,
                Explorer,
                new ProjectBuildConfigurationUtility(active, new TestLogger<ProjectBuildConfigurationUtility>()),
                Paths,
                new TestEngineSettingsProvider(EngineIncludePath),
                Logger);
        }

        public void CreateBuildArtifacts(string? mapJson = null)
        {
            var buildOutput = Paths.GetBuildOutputDirectory(BuildConfigurationEnum.Debug);
            Directory.CreateDirectory(buildOutput);
            File.WriteAllBytes(Paths.GetBuildOutputExePath(BuildConfigurationEnum.Debug), new byte[] { 7, 8, 9 });
            File.WriteAllBytes(Path.Combine(buildOutput, "Game.dll"), new byte[] { 4, 5 });
            Directory.CreateDirectory(Path.Combine(buildOutput, "private"));
            File.WriteAllText(Path.Combine(buildOutput, "private", "nested.txt"), "excluded");

            var resources = Paths.GetResourcesDirectory();
            Directory.CreateDirectory(resources);
            File.WriteAllBytes(Path.Combine(resources, "assets.bin"), new byte[] { 1, 2, 3, 4, 5, 6, 7 });
            File.WriteAllBytes(Path.Combine(resources, "map.bin"), new byte[] { 9, 10 });
            File.WriteAllText(Path.Combine(resources, "map.json"), mapJson ?? """
                {"Assets":[
                  {"Name":"Level.SCENE","AssetPath":"C:/Root/Project/Scenes/Level.SCENE","Offset":0},
                  {"Name":"Brick.png","AssetPath":"C:/Root/Project/Textures/Brick.png","Offset":3}
                ]}
                """);
            Directory.CreateDirectory(Path.Combine(resources, "Cache"));
            File.WriteAllBytes(Path.Combine(resources, "Cache", "cached.bin"), new byte[] { 11 });
            Directory.CreateDirectory(Path.Combine(resources, "crash_reports"));
            File.WriteAllBytes(Path.Combine(resources, "crash_reports", "dump.bin"), new byte[] { 12 });
        }

        public void Dispose() => Fixture.Dispose();
    }

    /// <summary>Successful Debug packaging routes clean solution and forced asset stages, copies exact bytes, reports assets and restarts editor last.</summary>
    [Fact]
    public async Task SuccessPackagesRequiredFilesAndReportsAssets()
    {
        using var context = new TestContext();
        Directory.CreateDirectory(context.OutputPath);
        File.WriteAllText(Path.Combine(context.OutputPath, "stale.txt"), "stale");
        var progress = new List<ProjectBuildProgress>();
        context.Build.OnBuild = request =>
        {
            if (request.BuildAssets)
            {
                request.Progress!(new AssetBuildProgressInfo(1, 2, "Project/Textures/Brick.png"));
                context.CreateBuildArtifacts();
            }
            return Task.FromResult(true);
        };
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await context.Service.BuildAsync(
            new ProjectBuildRequest(BuildConfigurationEnum.Debug, context.OutputPath, true, string.Empty),
            progress.Add,
            CancellationToken.None);
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsCancelled);
        Assert.Equal(new[] { "solution", "assets", "explorer", "editor" }, context.Calls);
        Assert.Equal(context.OutputPath, Assert.Single(context.Explorer.OpenedDirectories));
        Assert.Equal(1, context.Editor.StartCalls);
        Assert.Equal(2, context.Build.Requests.Count);
        Assert.Equal((true, true, false, true, false), (context.Build.Requests[0].ForceSolution, context.Build.Requests[0].CleanSolution, context.Build.Requests[0].ForceAssets, context.Build.Requests[0].BuildSolution, context.Build.Requests[0].BuildAssets));
        Assert.Equal((false, false, true, false, true), (context.Build.Requests[1].ForceSolution, context.Build.Requests[1].CleanSolution, context.Build.Requests[1].ForceAssets, context.Build.Requests[1].BuildSolution, context.Build.Requests[1].BuildAssets));
        Assert.Contains(progress, item => item.Status == "Building asset 1/2: Brick.png" && item.CurrentStep == 3 && item.TotalSteps == 6);
        Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(Path.Combine(context.OutputPath, "Game.exe")));
        Assert.Equal(new byte[] { 4, 5 }, File.ReadAllBytes(Path.Combine(context.OutputPath, "Game.dll")));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, File.ReadAllBytes(Path.Combine(context.OutputPath, "Resources", "assets.bin")));
        Assert.Equal(new byte[] { 9, 10 }, File.ReadAllBytes(Path.Combine(context.OutputPath, "Resources", "map.bin")));
        Assert.False(File.Exists(Path.Combine(context.OutputPath, "stale.txt")));
        Assert.False(Directory.Exists(Path.Combine(context.OutputPath, "private")));
        Assert.False(Directory.Exists(Path.Combine(context.OutputPath, "Resources", "Cache")));
        Assert.False(Directory.Exists(Path.Combine(context.OutputPath, "Resources", "crash_reports")));

        var report = File.ReadAllText(Path.Combine(context.OutputPath, "BuildResult_DO_NOT_SHIP.txt"));
        Assert.Contains("- Total assets: 2", report);
        Assert.Contains("3 B | Level.SCENE | Scenes\\Level.SCENE", report);
        Assert.Contains("4 B | Brick.png | Textures\\Brick.png", report);
        Assert.Contains("- Scenes built: 1", report);
        Assert.Contains("- Scenes\\Level.SCENE", report);
        Assert.Contains("Cache and crash_reports are intentionally excluded", report);
        var timestampText = report.Split(Environment.NewLine).Single(line => line.StartsWith("Timestamp (UTC): "))[17..];
        var timestamp = DateTime.ParseExact(timestampText, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        Assert.InRange(timestamp, before, after);
        var durationText = report.Split(Environment.NewLine).Single(line => line.StartsWith("Build Duration: "))[16..].Replace(" seconds", string.Empty);
        var duration = double.Parse(durationText, CultureInfo.InvariantCulture);
        Assert.True(double.IsFinite(duration));
        Assert.True(duration >= 0);
    }

    /// <summary>A failed solution build returns failure and skips assets, packaging, Explorer and editor restart.</summary>
    [Fact]
    public async Task SolutionFailureStopsRemainingStages()
    {
        using var context = new TestContext();
        context.Build.OnBuild = _ => Task.FromResult(false);

        var result = await context.Service.BuildAsync(
            new ProjectBuildRequest(BuildConfigurationEnum.Debug, context.OutputPath, false, string.Empty),
            _ => { },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsCancelled);
        Assert.Contains("Build failed", result.ErrorMessage);
        Assert.Equal(new[] { "solution" }, context.Calls);
        Assert.False(Directory.Exists(context.OutputPath));
    }

    /// <summary>Malformed asset map fails during reporting after package copy and never opens Explorer or restarts editor.</summary>
    [Fact]
    public async Task MalformedMapFailsBeforeSuccessActions()
    {
        using var context = new TestContext();
        context.Build.OnBuild = request =>
        {
            if (request.BuildAssets) context.CreateBuildArtifacts("not json");
            return Task.FromResult(true);
        };

        var result = await context.Service.BuildAsync(
            new ProjectBuildRequest(BuildConfigurationEnum.Debug, context.OutputPath, false, string.Empty),
            _ => { },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsCancelled);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Equal(new[] { "solution", "assets" }, context.Calls);
        Assert.True(File.Exists(Path.Combine(context.OutputPath, "Game.exe")));
        Assert.False(File.Exists(Path.Combine(context.OutputPath, "BuildResult_DO_NOT_SHIP.txt")));
    }

    /// <summary>Missing engine artifacts fail before compiler calls and external success actions.</summary>
    [Fact]
    public async Task MissingEngineArtifactStopsBeforeBuild()
    {
        using var context = new TestContext();
        File.Delete(Path.Combine(context.EngineIncludePath, "Rei.lib"));

        var result = await context.Service.BuildAsync(
            new ProjectBuildRequest(BuildConfigurationEnum.Debug, context.OutputPath, false, string.Empty),
            _ => { },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Rei.lib", result.ErrorMessage);
        Assert.Empty(context.Calls);
        Assert.Empty(context.Build.Requests);
    }

    /// <summary>Unsupported configuration and empty output are rejected before build invocation.</summary>
    [Theory]
    [InlineData(BuildConfigurationEnum.EditorDebug, "output", "Only Debug and Release")]
    [InlineData(BuildConfigurationEnum.Debug, " ", "Output path is required")]
    public async Task InvalidRequestStopsBeforeBuild(BuildConfigurationEnum configuration, string output, string expectedError)
    {
        using var context = new TestContext();
        var outputPath = string.IsNullOrWhiteSpace(output) ? output : context.Fixture.Directory.GetPath(output);

        var result = await context.Service.BuildAsync(new ProjectBuildRequest(configuration, outputPath, false, string.Empty), _ => { }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedError, result.ErrorMessage);
        Assert.Empty(context.Build.Requests);
    }

    /// <summary>A pre-cancelled request reports cancellation before progress, file changes or collaborators.</summary>
    [Fact]
    public async Task PreCancelledRequestHasNoSideEffects()
    {
        using var context = new TestContext();
        var progressCalls = 0;

        var result = await context.Service.BuildAsync(
            new ProjectBuildRequest(BuildConfigurationEnum.Debug, context.OutputPath, false, string.Empty),
            _ => progressCalls++,
            new CancellationToken(canceled: true));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(0, progressCalls);
        Assert.Empty(context.Calls);
        Assert.False(Directory.Exists(context.OutputPath));
    }

    /// <summary>A progress callback exception becomes a failed result before settings or build stages run.</summary>
    [Fact]
    public async Task ProgressCallbackFailureStopsBuild()
    {
        using var context = new TestContext();
        var failure = new InvalidOperationException("controlled progress failure");

        var result = await context.Service.BuildAsync(
            new ProjectBuildRequest(BuildConfigurationEnum.Debug, context.OutputPath, false, string.Empty),
            _ => throw failure,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsCancelled);
        Assert.Equal(failure.Message, result.ErrorMessage);
        Assert.Empty(context.Build.Requests);
        Assert.Contains(context.Logger.Entries, entry => ReferenceEquals(entry.Exception, failure));
    }
}
