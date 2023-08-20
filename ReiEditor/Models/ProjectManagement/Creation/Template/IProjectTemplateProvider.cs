using System.Threading.Tasks;

namespace ReiEditor.Models.ProjectManagement.Creation.Template;

public interface IProjectTemplateProvider
{
	Task<string> GetVSSolutionTemplate();
	Task<string> GetVSProjectTemplate();
}