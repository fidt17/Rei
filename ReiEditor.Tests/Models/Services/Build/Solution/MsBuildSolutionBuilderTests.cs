using Newtonsoft.Json.Linq;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Solution;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build.Solution;

/// <summary>Verifies invalid compiler paths are rejected before any build process can start.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class MsBuildSolutionBuilderTests
{
    /// <summary>Missing, empty and directory compiler paths fail at the path guard without creating build output.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("directory")]
    public async Task InvalidCompilerPathFailsBeforeProcessCreation(string pathKind)
    {
        using var fixture = new TemporaryProjectFixture();
        var compiler = pathKind switch
        {
            "empty" => "",
            "directory" => fixture.Directory.RootPath,
            _ => fixture.Directory.GetPath("Missing", "MSBuild.exe")
        };
        var stored = new JObject { [nameof(EditorPreferences.MsBuildPath)] = compiler }.ToString();
        var preferences = new EditorPreferencesService(new TestEditorStorageService(_ => Task.FromResult<string?>(stored)),
            new TestLogger<EditorPreferencesService>(), new JsonSerializer());
        await preferences.InitializeAsync();
        var active = new ActiveProjectService(new TestLogger<ActiveProjectService>());
        active.OpenProject(fixture.Project);
        var builder = new MsBuildSolutionBuilder(fixture.Resources, preferences, active, new TestLogger<MsBuildSolutionBuilder>());

        var failure = await Assert.ThrowsAsync<Exception>(() => builder.Build(BuildConfigurationEnum.EditorDebug));

        Assert.Equal("Invalid MsBuild path", failure.Message);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Directory.RootPath));
    }
}
