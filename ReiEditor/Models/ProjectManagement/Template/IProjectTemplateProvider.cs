using System.Threading.Tasks;

namespace ReiEditor.Models.ProjectManagement.Template;

public interface IProjectTemplateProvider
{
	Task<string> GetVSSolutionTemplate();
	Task<string> GetVSProjectTemplate();
	Task<string> GetMainFileTemplate();
	Task<string> GetNewShaderTemplate();
}
