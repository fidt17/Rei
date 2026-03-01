using System;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Editor;

namespace ReiEditor.Models.ProjectManagement.Template;

public class ProjectTemplateProvider : IProjectTemplateProvider
{
	private readonly IEditorResourceService _editorResourceService;

	public ProjectTemplateProvider(IEditorResourceService editorResourceService)
	{
		_editorResourceService = editorResourceService;
	}

	public async Task<string> GetVSSolutionTemplate()
	{
		var result = await _editorResourceService.Load("ProjectTemplates", "SolutionTemplate", "sln_template.txt");
		if (string.IsNullOrWhiteSpace(result)) throw new Exception("Could not load solution template");
		return result;
	}

	public async Task<string> GetVSProjectTemplate()
	{
		var result = await _editorResourceService.Load("ProjectTemplates", "SolutionTemplate", "proj_template.txt");
		if (string.IsNullOrWhiteSpace(result)) throw new Exception("Could not load project template");
		return result;
	}

	public async Task<string> GetMainFileTemplate()
	{
		var result = await _editorResourceService.Load("ProjectTemplates", "SolutionTemplate", "main_cpp_template.txt");
		if (string.IsNullOrWhiteSpace(result)) throw new Exception("Could not load main file template");
		return result;
	}

	public async Task<string> GetNewShaderTemplate()
	{
		var result = await _editorResourceService.Load("ProjectTemplates", "new_shader_template.rshader");
		if (string.IsNullOrWhiteSpace(result)) throw new Exception("Could not load new shader template");
		return result;
	}
}
