using Editor.Models.Services.Logging;
using Editor.Utils.Factory;

namespace Editor.ViewModels;

public class ProjectManagementWindowViewModel : BaseViewModel
{
	public TabContainer TabContainer { get; } = new();

	private readonly ILogger<ProjectManagementWindowViewModel> _logger;
	private readonly IFactory<ProjectsListTabViewModel> _projectsListViewModelFactory;
	private readonly IFactory<ProjectCreationTabViewModel> _projectCreationViewModelFactory;

#pragma warning disable CS8618
	public ProjectManagementWindowViewModel() { }
#pragma warning restore CS8618

	public ProjectManagementWindowViewModel(
		ILogger<ProjectManagementWindowViewModel> logger, 
		IFactory<ProjectsListTabViewModel> projectsListViewModelFactory,
		IFactory<ProjectCreationTabViewModel> projectCreationViewModelFactory)
	{
		_logger = logger;
		_projectsListViewModelFactory = projectsListViewModelFactory;
		_projectCreationViewModelFactory = projectCreationViewModelFactory;

		TabContainer.LogOnTabChange(_logger);
	}

	public void OpenProjectsListTab()
	{
		var projectsListViewModel = _projectsListViewModelFactory.CreateInstance();
		TabContainer.OpenTab(projectsListViewModel);

		projectsListViewModel.CreateProjectCommand.ExecutedEvent += OpenCreateProjectTab;
	}

	public void OpenCreateProjectTab()
	{
		var projectCreationViewModel = _projectCreationViewModelFactory.CreateInstance();
		TabContainer.OpenTab(projectCreationViewModel);

		projectCreationViewModel.CancelProjectCreationCommand.ExecutedEvent += OpenProjectsListTab;
	}
}