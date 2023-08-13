using System.Threading.Tasks;
using Autofac;
using Editor.Models.ProjectManagement.BookmarkedProjects;
using Editor.Models.ProjectManagement.Creation;
using Editor.Models.ProjectManagement.Deletion;
using Editor.Startup.Common;
using Editor.Startup.EntryPoints;
using Editor.Utils.Extensions;
using Editor.ViewModels;
using Editor.ViewModels.Commands;

namespace Editor.Startup.Scopes;

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
		b.RegisterType<ProjectsListTabViewModel>();
		b.RegisterType<ProjectCreationTabViewModel>();
		b.RegisterType<OpenProjectCommand>();

		b.RegisterType<ProjectsListElementViewModel>();
	}
}