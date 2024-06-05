using System.Threading.Tasks;

namespace ReiEditor.Models.ProjectManagement.Update;

public interface IProjectUpdateService
{
    Task UpdateProject(Project project);
}