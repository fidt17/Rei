using System;
using System.Threading.Tasks;

namespace Editor.Models.ProjectManagement.Creation;

public interface IProjectCreationService
{
	event Action<Project> ProjectCreatedEvent;
	event Action ProjectCreationFailedEvent;
	
	ProjectCreationConfiguration Configuration { get; }
	ProjectCreationValidator Validator { get; }

	Task<Project?> CreateProject();
}