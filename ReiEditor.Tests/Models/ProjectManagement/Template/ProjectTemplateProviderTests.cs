using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Resources.Editor;

namespace ReiEditor.Tests.Models.ProjectManagement.Template;

/// <summary>Verifies editor resource routing and missing project template failures.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "ProjectManagement")]
public sealed class ProjectTemplateProviderTests
{
    /// <summary>Records editor resource requests and returns configured template text.</summary>
    private sealed class TestEditorResourceService : IEditorResourceService
    {
        public string? Result { get; set; } = "template";
        public List<string[]> Requests { get; } = new();

        public Task<string?> Load(params string[] path)
        {
            Requests.Add(path);
            return Task.FromResult(Result);
        }
    }

    /// <summary>Each template method loads its exact embedded resource path.</summary>
    [Fact]
    public async Task TestLoadsTemplatesFromExpectedResourcePaths()
    {
        var resources = new TestEditorResourceService();
        var provider = new ProjectTemplateProvider(resources);

        Assert.Equal("template", await provider.GetVSSolutionTemplate());
        Assert.Equal("template", await provider.GetVSProjectTemplate());
        Assert.Equal("template", await provider.GetMainFileTemplate());
        Assert.Equal("template", await provider.GetNewShaderTemplate());

        Assert.Collection(
            resources.Requests,
            path => Assert.Equal(new[] { "ProjectTemplates", "SolutionTemplate", "sln_template.txt" }, path),
            path => Assert.Equal(new[] { "ProjectTemplates", "SolutionTemplate", "proj_template.txt" }, path),
            path => Assert.Equal(new[] { "ProjectTemplates", "SolutionTemplate", "main_cpp_template.txt" }, path),
            path => Assert.Equal(new[] { "ProjectTemplates", "new_shader_template.rshader" }, path));
    }

    /// <summary>Null, empty, and whitespace resource results fail instead of returning unusable templates.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TestRejectsMissingTemplateContent(string? content)
    {
        var resources = new TestEditorResourceService { Result = content };
        var provider = new ProjectTemplateProvider(resources);

        await Assert.ThrowsAsync<Exception>(() => provider.GetVSSolutionTemplate());
    }
}
