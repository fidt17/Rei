using ReiEditor.Models.Services.Logging;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.ProjectManagement;

public class ProjectManagementWindowViewModel : BaseViewModel
{
	public NavigationStore ActiveTab { get; } = new();

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

		ActiveTab.LogOnNavigate(_logger);
	}

	public void OpenProjectsListTab()
	{
		var projectsListViewModel = ActiveTab.Navigate(_projectsListViewModelFactory);
		projectsListViewModel.CreateProjectCommand.ExecutedEvent += OpenCreateProjectTab;
	}

	public void OpenCreateProjectTab()
	{
		var projectCreationViewModel = ActiveTab.Navigate(_projectCreationViewModelFactory);
		projectCreationViewModel.CancelProjectCreationCommand.ExecutedEvent += OpenProjectsListTab;
		projectCreationViewModel.CreateProjectCommand.ExecutedCommandEvent += HandleExecutedCreateProjectCommand;
	}

	private void HandleExecutedCreateProjectCommand(bool didCreate)
	{
		if (!didCreate) return;
		OpenProjectsListTab();
	}
}