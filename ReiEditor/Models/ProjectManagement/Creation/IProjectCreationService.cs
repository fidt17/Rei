using System;
using System.Threading.Tasks;

namespace ReiEditor.Models.ProjectManagement.Creation;

public interface IProjectCreationService
{
	event Action<Project> ProjectCreatedEvent;
	event Action ProjectCreationFailedEvent;
	
	ProjectCreationConfiguration Configuration { get; }
	ProjectCreationValidator Validator { get; }

	Task<Project?> CreateProject();
}