using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.ProjectBuild;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.Services.Build.ProjectBuild;

/// <summary>Verifies package, executable, and resource output path derivation.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Build")]
public sealed class ProjectBuildOutputPathUtilityTests : IDisposable
{
    /// <summary>Provides fixed active project for path-only tests.</summary>
    private sealed class TestActiveProjectService(Project project) : IActiveProjectService
    {
        public event Action<Project>? ActiveProjectChangedEvent;
        public Project GetActiveProject() => project;
        public void OpenProject(Project value) => ActiveProjectChangedEvent?.Invoke(value);
    }

    private readonly TemporaryProjectFixture _project = new();

    /// <summary>Debug and release configurations use distinct default package folders.</summary>
    [Theory]
    [InlineData(BuildConfigurationEnum.Debug, "Debug Build")]
    [InlineData(BuildConfigurationEnum.EditorDebug, "Debug Build")]
    [InlineData(BuildConfigurationEnum.Release, "Release Build")]
    [InlineData(BuildConfigurationEnum.EditorRelease, "Debug Build")]
    public void TestDefaultPackageOutputPathUsesSupportedConfigurationMapping(BuildConfigurationEnum configuration, string folder)
    {
        var utility = CreateUtility();

        Assert.Equal(_project.Directory.GetPath("Builds", folder), utility.GetDefaultPackageOutputPath(configuration));
    }

    /// <summary>Build output and executable paths include native configuration and project name.</summary>
    [Theory]
    [InlineData(BuildConfigurationEnum.Debug, "x64Debug")]
    [InlineData(BuildConfigurationEnum.Release, "x64Release")]
    public void TestBuildOutputPathsUseNativeConfiguration(BuildConfigurationEnum configuration, string nativeConfiguration)
    {
        var utility = CreateUtility();
        var expectedDirectory = _project.Directory.GetPath("bin", nativeConfiguration, "Test project");

        Assert.Equal(expectedDirectory, utility.GetBuildOutputDirectory(configuration));
        Assert.Equal(Path.Combine(expectedDirectory, "Test project.exe"), utility.GetBuildOutputExePath(configuration));
    }

    /// <summary>Resources path is shared under project bin root.</summary>
    [Fact]
    public void TestResourcesDirectoryUsesProjectBinRoot()
    {
        Assert.Equal(_project.Directory.GetPath("bin", "Resources"), CreateUtility().GetResourcesDirectory());
    }

    /// <summary>Creates path utility using isolated resource and project services.</summary>
    private ProjectBuildOutputPathUtility CreateUtility()
    {
        return new ProjectBuildOutputPathUtility(new TestActiveProjectService(_project.Project), _project.Resources);
    }

    /// <summary>Deletes isolated project tree.</summary>
    public void Dispose() => _project.Dispose();
}
