using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Build;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.Services.Build;

/// <summary>Verifies live, staging, seed, validation, promotion, and cleanup output behavior.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class EditorBuildOutputServiceTests : IDisposable
{
    /// <summary>Provides fixed active project without touching editor profile state.</summary>
    private sealed class TestActiveProjectService(Project project) : IActiveProjectService
    {
        public event Action<Project>? ActiveProjectChangedEvent;
        public Project GetActiveProject() => project;
        public void OpenProject(Project value) => ActiveProjectChangedEvent?.Invoke(value);
    }

    private readonly TemporaryDirectory _directory = new();

    /// <summary>Live output paths derive from active project root and name.</summary>
    [Fact]
    public void TestGetLiveOutputDerivesExpectedLayout()
    {
        var service = CreateService("Space Game");

        var output = service.GetLiveOutput();

        Assert.Equal(_directory.RootPath, output.StagingRootPath);
        Assert.Equal(_directory.GetPath(ResourceConstants.BIN_DIR_NAME), output.BinDirectoryPath);
        Assert.Equal(_directory.GetPath(ResourceConstants.BIN_DIR_NAME, "x64EditorDebug", "Space Game"), output.ClientOutputDirectoryPath);
        Assert.Equal(_directory.GetPath(ResourceConstants.BIN_DIR_NAME, "x64EditorDebug", "Space Game", "Space Game.dll"), output.ClientDllPath);
        Assert.Equal(_directory.GetPath(ResourceConstants.BIN_DIR_NAME, ResourceConstants.RESOURCES_DIR_NAME), output.ResourcesDirectoryPath);
    }

    /// <summary>Preparing staging output removes stale staging content and creates both output directories.</summary>
    [Fact]
    public void TestPrepareStagingOutputRecreatesCleanDirectories()
    {
        var service = CreateService();
        var stale = _directory.GetPath(".rei_tmp", "editor_build_staging", "stale.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "stale");

        var output = service.PrepareStagingOutput();

        Assert.False(File.Exists(stale));
        Assert.True(Directory.Exists(output.ClientOutputDirectoryPath));
        Assert.True(Directory.Exists(output.ResourcesDirectoryPath));
        Assert.StartsWith(_directory.GetPath(".rei_tmp", "editor_build_staging"), output.BinDirectoryPath);
    }

    /// <summary>Seed recursively copies live client files into staging and overwrites prior bytes.</summary>
    [Fact]
    public void TestSeedStagingClientOutputCopiesRecursively()
    {
        var service = CreateService();
        var live = service.GetLiveOutput();
        var staging = service.PrepareStagingOutput();
        var liveFile = Path.Combine(live.ClientOutputDirectoryPath, "nested", "dependency.dll");
        var stagedFile = Path.Combine(staging.ClientOutputDirectoryPath, "nested", "dependency.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(liveFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedFile)!);
        File.WriteAllBytes(liveFile, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(stagedFile, new byte[] { 9 });

        service.SeedStagingClientOutputFromLive(staging);

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(stagedFile));
    }

    /// <summary>Promotion validates all staging inputs before changing any live file.</summary>
    [Fact]
    public void TestPromotionValidationRunsBeforeLiveCopy()
    {
        var service = CreateService();
        var live = service.GetLiveOutput();
        var staging = service.PrepareStagingOutput();
        Directory.CreateDirectory(live.ClientOutputDirectoryPath);
        File.WriteAllText(live.ClientDllPath, "live");

        Assert.Throws<Exception>(() => service.PromoteStagingOutput(staging));

        Assert.Equal("live", File.ReadAllText(live.ClientDllPath));
    }

    /// <summary>Valid staging promotion copies client and resource trees into live output.</summary>
    [Fact]
    public void TestPromoteStagingOutputCopiesValidatedTrees()
    {
        var service = CreateService();
        var staging = service.PrepareStagingOutput();
        File.WriteAllBytes(staging.ClientDllPath, new byte[] { 4, 5 });
        File.WriteAllText(Path.Combine(staging.ResourcesDirectoryPath, "assets.bin"), "assets");

        service.PromoteStagingOutput(staging);

        var live = service.GetLiveOutput();
        Assert.Equal(new byte[] { 4, 5 }, File.ReadAllBytes(live.ClientDllPath));
        Assert.Equal("assets", File.ReadAllText(Path.Combine(live.ResourcesDirectoryPath, "assets.bin")));
    }

    /// <summary>Creates output service around isolated active project.</summary>
    private EditorBuildOutputService CreateService(string projectName = "Game")
    {
        var project = new Project();
        project.SetProjectName(projectName);
        project.SetProjectFilePath(_directory.GetPath($"{projectName}.reiproj"));
        return new EditorBuildOutputService(new TestActiveProjectService(project));
    }

    /// <summary>Deletes isolated output tree.</summary>
    public void Dispose() => _directory.Dispose();
}
