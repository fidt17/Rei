using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Resources;

namespace ReiEditor.Models.ProjectManagement.Creation.Template;

public class ProjectTemplateProvider : IProjectTemplateProvider
{
	private readonly IResourceLoader _resourceLoader;

	public ProjectTemplateProvider(IResourceLoader resourceLoader)
	{
		_resourceLoader = resourceLoader;
	}

	public async Task<string> GetVSSolutionTemplate()
	{
		var result = await _resourceLoader.Load("ProjectTemplates", "SolutionTemplate", "sln_template.txt");
		if (string.IsNullOrWhiteSpace(result)) throw new Exception("Could not load solution template");
		return result;
	}

	public async Task<string> GetVSProjectTemplate()
	{
		var result = await _resourceLoader.Load("ProjectTemplates", "SolutionTemplate", "proj_template.txt");
		if (string.IsNullOrWhiteSpace(result)) throw new Exception("Could not load project template");
		return result;
	}
}