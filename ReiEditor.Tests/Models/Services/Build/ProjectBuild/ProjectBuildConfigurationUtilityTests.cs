using System.Xml.Linq;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.ProjectBuild;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build.ProjectBuild;

/// <summary>Verifies executable subsystem, entry point, and icon project-file updates.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class ProjectBuildConfigurationUtilityTests : IDisposable
{
    /// <summary>Provides fixed active project without loading editor state.</summary>
    private sealed class TestActiveProjectService(Project project) : IActiveProjectService
    {
        public event Action<Project>? ActiveProjectChangedEvent;
        public Project GetActiveProject() => project;
        public void OpenProject(Project value) => ActiveProjectChangedEvent?.Invoke(value);
    }

    private const string PROJECT_XML = """
        <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
            <ClCompile><WarningLevel>Level4</WarningLevel></ClCompile>
            <Link><SubSystem>Console</SubSystem><AdditionalDependencies>keep.lib</AdditionalDependencies></Link>
          </ItemDefinitionGroup>
          <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'">
            <Link><SubSystem>Console</SubSystem></Link>
          </ItemDefinitionGroup>
          <ItemGroup><ResourceCompile Include="Existing.rc" /></ItemGroup>
          <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
        </Project>
        """;

    private readonly TemporaryDirectory _directory = new();

    /// <summary>Windows executable settings update only selected configuration and preserve unrelated XML.</summary>
    [Fact]
    public void TestApplyWindowsSettingsPreservesUnrelatedConfigurationAndElements()
    {
        var (utility, path) = CreateUtility();

        utility.ApplyExecutableBuildSettings(new ProjectBuildRequest(BuildConfigurationEnum.Debug, "unused", false, ""));

        var doc = XDocument.Load(path);
        var groups = doc.Descendants().Where(x => x.Name.LocalName == "ItemDefinitionGroup").ToList();
        var debugLink = groups[0].Elements().Single(x => x.Name.LocalName == "Link");
        var releaseLink = groups[1].Elements().Single(x => x.Name.LocalName == "Link");
        Assert.Equal("Windows", debugLink.Elements().Single(x => x.Name.LocalName == "SubSystem").Value);
        Assert.Equal("mainCRTStartup", debugLink.Elements().Single(x => x.Name.LocalName == "EntryPointSymbol").Value);
        Assert.Equal("keep.lib", debugLink.Elements().Single(x => x.Name.LocalName == "AdditionalDependencies").Value);
        Assert.Equal("Console", releaseLink.Elements().Single(x => x.Name.LocalName == "SubSystem").Value);
    }

    /// <summary>Console setting removes generated entry point while retaining other linker settings.</summary>
    [Fact]
    public void TestApplyConsoleSettingsRemovesEntryPointOnly()
    {
        var (utility, path) = CreateUtility();
        utility.ApplyExecutableBuildSettings(new ProjectBuildRequest(BuildConfigurationEnum.Debug, "unused", false, ""));

        utility.ApplyExecutableBuildSettings(new ProjectBuildRequest(BuildConfigurationEnum.Debug, "unused", true, ""));

        var debugLink = XDocument.Load(path).Descendants().First(x => x.Name.LocalName == "Link");
        Assert.Equal("Console", debugLink.Elements().Single(x => x.Name.LocalName == "SubSystem").Value);
        Assert.DoesNotContain(debugLink.Elements(), x => x.Name.LocalName == "EntryPointSymbol");
        Assert.Contains(debugLink.Elements(), x => x.Name.LocalName == "AdditionalDependencies");
    }

    /// <summary>Icon application copies exact bytes, creates RC text, and inserts one include across repeated calls.</summary>
    [Fact]
    public void TestApplyIconCreatesFilesAndIdempotentInclude()
    {
        var (utility, projectPath) = CreateUtility();
        var iconPath = _directory.GetPath("Source Icon.ico");
        File.WriteAllBytes(iconPath, new byte[] { 0, 1, 2, 255 });
        var request = new ProjectBuildRequest(BuildConfigurationEnum.Release, "unused", false, iconPath);

        utility.ApplyExecutableBuildSettings(request);
        utility.ApplyExecutableBuildSettings(request);

        var generated = _directory.GetPath("Build", "Generated");
        Assert.Equal(new byte[] { 0, 1, 2, 255 }, File.ReadAllBytes(Path.Combine(generated, "AppIcon.ico")));
        Assert.Equal("IDI_APP_ICON ICON \"AppIcon.ico\"\r\n", File.ReadAllText(Path.Combine(generated, "AppIcon.rc")));
        var generatedIncludes = XDocument.Load(projectPath).Descendants()
            .Where(x => x.Name.LocalName == "ResourceCompile" && string.Equals(x.Attribute("Include")?.Value, @"Build\Generated\AppIcon.rc", StringComparison.OrdinalIgnoreCase));
        Assert.Single(generatedIncludes);
    }

    /// <summary>Blank icon removes only generated include and files while preserving unrelated resource entries.</summary>
    [Fact]
    public void TestBlankIconRemovesOnlyGeneratedArtifacts()
    {
        var (utility, projectPath) = CreateUtility();
        var iconPath = _directory.GetPath("icon.ico");
        File.WriteAllBytes(iconPath, new byte[] { 7 });
        utility.ApplyExecutableBuildSettings(new ProjectBuildRequest(BuildConfigurationEnum.Debug, "unused", false, iconPath));

        utility.ApplyExecutableBuildSettings(new ProjectBuildRequest(BuildConfigurationEnum.Debug, "unused", false, "  "));

        var doc = XDocument.Load(projectPath);
        Assert.Contains(doc.Descendants(), x => x.Name.LocalName == "ResourceCompile" && x.Attribute("Include")?.Value == "Existing.rc");
        Assert.DoesNotContain(doc.Descendants(), x => x.Name.LocalName == "ResourceCompile" && x.Attribute("Include")?.Value.Contains("AppIcon.rc") == true);
        Assert.False(File.Exists(_directory.GetPath("Build", "Generated", "AppIcon.ico")));
        Assert.False(File.Exists(_directory.GetPath("Build", "Generated", "AppIcon.rc")));
    }

    /// <summary>Missing project and missing icon paths fail before producing generated output.</summary>
    [Fact]
    public void TestMissingInputsThrowWithoutGeneratedFiles()
    {
        var (utility, projectPath) = CreateUtility();
        Assert.Throws<Exception>(() => utility.ApplyExecutableBuildSettings(
            new ProjectBuildRequest(BuildConfigurationEnum.Debug, "unused", false, _directory.GetPath("missing.ico"))));
        Assert.False(Directory.Exists(_directory.GetPath("Build", "Generated")));

        File.Delete(projectPath);
        Assert.Throws<Exception>(() => utility.ApplyExecutableBuildSettings(
            new ProjectBuildRequest(BuildConfigurationEnum.Debug, "unused", false, "")));
    }

    /// <summary>Malformed project XML throws and leaves source bytes unchanged.</summary>
    [Fact]
    public void TestMalformedProjectThrowsWithoutOverwrite()
    {
        var (utility, projectPath) = CreateUtility("<Project>");

        Assert.ThrowsAny<Exception>(() => utility.ApplyExecutableBuildSettings(
            new ProjectBuildRequest(BuildConfigurationEnum.Debug, "unused", false, "")));

        Assert.Equal("<Project>", File.ReadAllText(projectPath));
    }

    /// <summary>Creates utility and minimal project file inside isolated directory.</summary>
    private (ProjectBuildConfigurationUtility Utility, string ProjectPath) CreateUtility(string xml = PROJECT_XML)
    {
        var projectPath = _directory.GetPath("Game.vcxproj");
        File.WriteAllText(projectPath, xml);
        var project = new Project();
        project.SetProjectName("Game");
        project.SetProjectFilePath(_directory.GetPath("Game.reiproj"));
        project.SetProjectVisualStudioProjectPath(projectPath);
        var utility = new ProjectBuildConfigurationUtility(
            new TestActiveProjectService(project),
            new TestLogger<ProjectBuildConfigurationUtility>());
        return (utility, projectPath);
    }

    /// <summary>Deletes isolated project and icon files.</summary>
    public void Dispose() => _directory.Dispose();
}
