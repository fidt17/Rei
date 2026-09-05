using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReiEditor.Models.ProjectManagement.Creation;
using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.ProjectManagement.Template;

/// <summary>Verifies solution generation, project template updates, and source item group maintenance.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "ProjectManagement")]
public sealed class SolutionGeneratorTests : IDisposable
{
    /// <summary>Supplies deterministic text templates.</summary>
    private sealed class TestProjectTemplateProvider : IProjectTemplateProvider
    {
        public string SolutionTemplate { get; set; } = "Name={0};Project={1};Solution={2}";
        public string ProjectTemplate { get; set; } = PROJECT_TEMPLATE;
        public string MainTemplate { get; set; } = "int main() { return 0; }";

        public Task<string> GetVSSolutionTemplate() => Task.FromResult(SolutionTemplate);
        public Task<string> GetVSProjectTemplate() => Task.FromResult(ProjectTemplate);
        public Task<string> GetMainFileTemplate() => Task.FromResult(MainTemplate);
        public Task<string> GetNewShaderTemplate() => throw new NotSupportedException();
    }

    /// <summary>Supplies deterministic engine template substitutions.</summary>
    private sealed class TestEngineSettingsProvider : IEngineSettingsProvider
    {
        public Task InitializeAsync() => throw new NotSupportedException();
        public string GetEnginePath() => throw new NotSupportedException();
        public string GetEngineDebugIncludeDir() => "debug-include";
        public string GetEngineReleaseIncludeDir() => "release-include";
        public string GetEngineSourceIncludes() => "source-includes";
        public string GetEngineResourcesDir() => throw new NotSupportedException();
        public string GetEngineBehavioursDir() => throw new NotSupportedException();
        public string GetEngineVersion() => throw new NotSupportedException();
    }

    private const string PROJECT_TEMPLATE = """
        <Project>
          <PropertyGroup>
            <ProjectGuid>{0}</ProjectGuid>
            <RootNamespace>{1}</RootNamespace>
            <DebugInclude>{2}</DebugInclude>
            <ReleaseInclude>{3}</ReleaseInclude>
            <SourceIncludes>{4}</SourceIncludes>
          </PropertyGroup>
          <ItemGroup Label="ClCompile">
          </ItemGroup>
          <ItemGroup Label="ClInclude">
          </ItemGroup>
        </Project>
        """;

    private readonly TemporaryDirectory _directory = new();

    /// <summary>Generation writes linked solution and project GUIDs plus main source file and engine values.</summary>
    [Fact]
    public async Task TestGeneratesLinkedSolutionProjectAndMainSource()
    {
        var configuration = CreateConfiguration("Game");
        var generator = CreateGenerator();

        var result = await generator.GenerateSolution(configuration);

        Assert.Equal(_directory.GetPath("Game", "Game.sln"), result.SolutionPath);
        Assert.Equal(
            Path.GetFullPath(_directory.GetPath("Game", "Project", "Scripts", "Game.vcxproj")),
            Path.GetFullPath(result.ProjectPath));
        var solution = await File.ReadAllTextAsync(result.SolutionPath);
        var project = await File.ReadAllTextAsync(result.ProjectPath);
        var projectGuid = Assert.Single(Regex.Matches(project, @"<ProjectGuid>(\{[0-9a-fA-F-]{36}\})</ProjectGuid>")).Groups[1].Value;
        Assert.Contains($"Name=Game;Project={projectGuid};Solution=", solution);
        Assert.Matches(@"Solution=\{[0-9a-fA-F-]{36}\}", solution);
        Assert.Contains("<RootNamespace>Game</RootNamespace>", project);
        Assert.Contains("<DebugInclude>debug-include</DebugInclude>", project);
        Assert.Contains("<ReleaseInclude>release-include</ReleaseInclude>", project);
        Assert.Contains("<SourceIncludes>source-includes</SourceIncludes>", project);
        Assert.Equal("int main() { return 0; }", await File.ReadAllTextAsync(_directory.GetPath("Game", "Project", "Scripts", "ReiApp.cpp")));
    }

    /// <summary>Project update preserves extracted GUID and name while refreshing engine substitutions.</summary>
    [Fact]
    public async Task TestUpdateProjectFilePreservesGuidAndName()
    {
        var projectPath = _directory.GetPath("Update.vcxproj");
        const string GUID = "{01234567-89AB-CDEF-0123-456789ABCDEF}";
        await File.WriteAllTextAsync(projectPath, $"<Project><ProjectGuid>{GUID}</ProjectGuid><RootNamespace>UpdatedGame</RootNamespace></Project>");
        var generator = CreateGenerator();

        await generator.UpdateProjectFile(projectPath);

        var project = await File.ReadAllTextAsync(projectPath);
        Assert.Contains($"<ProjectGuid>{GUID}</ProjectGuid>", project);
        Assert.Contains("<RootNamespace>UpdatedGame</RootNamespace>", project);
        Assert.Contains("<ReleaseInclude>release-include</ReleaseInclude>", project);
        Assert.Contains("<SourceIncludes>source-includes</SourceIncludes>", project);
    }

    /// <summary>Project update rejects files missing required GUID or root namespace values.</summary>
    [Theory]
    [InlineData("<Project><RootNamespace>Game</RootNamespace></Project>")]
    [InlineData("<Project><ProjectGuid>{ID}</ProjectGuid></Project>")]
    public async Task TestUpdateProjectFileRejectsMissingIdentity(string project)
    {
        var projectPath = _directory.GetPath("Malformed.vcxproj");
        await File.WriteAllTextAsync(projectPath, project);

        await Assert.ThrowsAsync<Exception>(() => CreateGenerator().UpdateProjectFile(projectPath));
    }

    /// <summary>Source update normalizes slashes, removes leading separators, filters extensions, and deduplicates casing.</summary>
    [Fact]
    public async Task TestAddSourceFilesNormalizesFiltersAndDeduplicatesIncludes()
    {
        var projectPath = await WriteProjectFile("Sources.vcxproj");
        var generator = CreateGenerator();

        await generator.AddSourceFiles(projectPath, new[]
        {
            "/Scripts/Player.cpp",
            @"scripts\PLAYER.cpp",
            @"\Scripts\Player.h",
            "Scripts/notes.txt",
            "scripts/NOTES.TXT",
            " ",
            null!
        });

        var project = await File.ReadAllTextAsync(projectPath);
        Assert.Single(Regex.Matches(project, "<ClCompile Include="));
        Assert.Single(Regex.Matches(project, "<ClInclude Include="));
        Assert.Contains("<ClCompile Include=\"Scripts\\Player.cpp\" />", project);
        Assert.Contains("<ClInclude Include=\"Scripts\\Player.h\" />", project);
        Assert.DoesNotContain("notes.txt", project);
    }

    /// <summary>Uppercase C++ extensions remain in their matching project item groups.</summary>
    [Fact]
    public async Task TestAddSourceFilesSupportsUppercaseExtensions()
    {
        var projectPath = await WriteProjectFile("Uppercase.vcxproj");

        await CreateGenerator().AddSourceFiles(projectPath, new[]
        {
            "Source.CPP",
            "source.cpp",
            "Mixed.CpP",
            "Header.H",
            "header.h",
            "Mixed.h",
            "Ignored.cpp.bak",
            "Ignored.hpp"
        });

        var project = await File.ReadAllTextAsync(projectPath);
        Assert.Equal(2, Regex.Matches(project, "<ClCompile Include=").Count);
        Assert.Equal(2, Regex.Matches(project, "<ClInclude Include=").Count);
        Assert.Contains("<ClCompile Include=\"Source.CPP\" />", project);
        Assert.Contains("<ClCompile Include=\"Mixed.CpP\" />", project);
        Assert.Contains("<ClInclude Include=\"Header.H\" />", project);
        Assert.Contains("<ClInclude Include=\"Mixed.h\" />", project);
        Assert.DoesNotContain("Ignored.cpp.bak", project);
        Assert.DoesNotContain("Ignored.hpp", project);
    }

    /// <summary>Source include paths round-trip through XML escaping without double escaping on repeated updates.</summary>
    [Fact]
    public async Task TestAddSourceFilesEscapesXmlIncludePaths()
    {
        var projectPath = await WriteProjectFile("Escaping.vcxproj");
        var generator = CreateGenerator();
        var includes = new[]
        {
            "Scripts/A&B.cpp",
            "Scripts/Unicode файл & header.h",
            "Scripts/Literal&amp;Entity.cpp"
        };

        await generator.AddSourceFiles(projectPath, includes);
        var first = await File.ReadAllTextAsync(projectPath);
        await generator.AddSourceFiles(projectPath, includes);
        var second = await File.ReadAllTextAsync(projectPath);

        var document = XDocument.Parse(second);
        Assert.Equal(
            new[] { @"Scripts\A&B.cpp", @"Scripts\Literal&amp;Entity.cpp" },
            document.Descendants("ClCompile").Select(x => x.Attribute("Include")!.Value).ToArray());
        Assert.Equal(@"Scripts\Unicode файл & header.h", Assert.Single(document.Descendants("ClInclude")).Attribute("Include")?.Value);
        Assert.Contains("A&amp;B.cpp", second);
        Assert.Contains("Unicode файл &amp; header.h", second);
        Assert.Contains("Literal&amp;amp;Entity.cpp", second);
        Assert.Equal(first, second);
    }

    /// <summary>Empty source list clears both managed item groups and remains stable on repeated updates.</summary>
    [Fact]
    public async Task TestAddSourceFilesClearsGroupsIdempotently()
    {
        var projectPath = await WriteProjectFile("Empty.vcxproj");
        var generator = CreateGenerator();
        await generator.AddSourceFiles(projectPath, new[] { "Old.cpp", "Old.h" });

        await generator.AddSourceFiles(projectPath, Array.Empty<string>());
        var first = await File.ReadAllTextAsync(projectPath);
        await generator.AddSourceFiles(projectPath, Array.Empty<string>());
        var second = await File.ReadAllTextAsync(projectPath);

        Assert.DoesNotContain("<ClCompile Include=", first);
        Assert.DoesNotContain("<ClInclude Include=", first);
        Assert.Equal(first, second);
    }

    /// <summary>Missing or unterminated managed item groups fail without overwriting original project text.</summary>
    [Theory]
    [InlineData("<Project><ItemGroup Label=\"ClInclude\"></ItemGroup></Project>")]
    [InlineData("<Project><ItemGroup Label=\"ClCompile\">")]
    public async Task TestAddSourceFilesRejectsMalformedGroupsWithoutWriting(string original)
    {
        var projectPath = _directory.GetPath("MalformedGroups.vcxproj");
        await File.WriteAllTextAsync(projectPath, original);

        await Assert.ThrowsAsync<Exception>(() => CreateGenerator().AddSourceFiles(projectPath, new[] { "New.cpp" }));

        Assert.Equal(original, await File.ReadAllTextAsync(projectPath));
    }

    /// <summary>Creates generator with deterministic fakes.</summary>
    private static SolutionGenerator CreateGenerator()
    {
        return new SolutionGenerator(
            new TestLogger<SolutionGenerator>(),
            new TestProjectTemplateProvider(),
            new TestEngineSettingsProvider());
    }

    /// <summary>Creates configuration rooted under isolated directory.</summary>
    private ProjectCreationConfiguration CreateConfiguration(string name)
    {
        return new ProjectCreationConfiguration
        {
            ParentDirectoryPath = _directory.RootPath,
            ProjectName = name
        };
    }

    /// <summary>Writes minimal project template used by source group updates.</summary>
    private async Task<string> WriteProjectFile(string name)
    {
        var path = _directory.GetPath(name);
        await File.WriteAllTextAsync(path, string.Format(PROJECT_TEMPLATE, "{ID}", "Game", "debug", "release", "source"));
        return path;
    }

    /// <summary>Deletes generated solution and project files.</summary>
    public void Dispose() => _directory.Dispose();
}
