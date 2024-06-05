using System.Threading.Tasks;
using Autofac;
using ReiEditor.Models.ProjectManagement.BookmarkedProjects;
using ReiEditor.Models.ProjectManagement.Creation;
using ReiEditor.Models.ProjectManagement.Deletion;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Settings.Commands;
using ReiEditor.ViewModels.Windows.ProjectManagement;
using ReiEditor.ViewModels.Windows.ProjectManagement.Commands;

namespace ReiEditor.Startup.Scopes;

public class ProjectManagementScope : BaseLifetimeScope
{
	public ProjectManagementScope(ApplicationScope parentScope) : base(nameof(ProjectManagementScope), parentScope) { }

	protected override Task OnScopeStart()
	{
		Scope.Resolve<ProjectManagementEntryPoint>().Start();
		
		return Task.CompletedTask;
	}

	protected override void ConfigureContainer(ContainerBuilder b)
	{
		b.RegisterInstance(this);

		b.RegisterType<ProjectCreationService>().As<IProjectCreationService>();
		b.RegisterSingleton<ProjectDeletionService>().As<IProjectDeletionService>();
		b.RegisterSingleton<BookmarkedProjectsService>().As<IBookmarkedProjectsService>();

		b.RegisterSingleton<ProjectManagementEntryPoint>();
		
		ConfigureViews(b);
	}

	private void ConfigureViews(ContainerBuilder b)
	{
		b.RegisterType<ProjectManagementWindowViewModel>();
		b.RegisterType<EditorSetupTabViewModel>();
		b.RegisterType<ProjectsListTabViewModel>();
		b.RegisterType<ProjectCreationTabViewModel>();

		b.RegisterType<SetEngineLocationCommand>();
		b.RegisterType<SetMsBuildLocationCommand>();
		b.RegisterType<OpenProjectCommand>();

		b.RegisterType<ProjectsListElementViewModel>();
	}
}