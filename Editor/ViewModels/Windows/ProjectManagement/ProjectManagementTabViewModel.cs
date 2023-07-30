using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Editor.Models.ProjectManagement;
using Editor.Models.Services.Logging;
using ReactiveUI;

namespace Editor.ViewModels;

public class ProjectManagementTabViewModel : BaseViewModel
{
	public ICommand OpenProjectCommand { get; }
	public ICommand CreateProjectCommand { get; }
	
	public ObservableCollection<Project> AvailableProjects { get; } = new();
	private readonly ILogger<ProjectManagementTabViewModel> _logger;

#pragma warning disable CS8618
	public ProjectManagementTabViewModel() { FillMockData(); }
#pragma warning restore CS8618

	public ProjectManagementTabViewModel(ILogger<ProjectManagementTabViewModel> logger)
	{
		_logger = logger;

		OpenProjectCommand = ReactiveCommand.Create(ShowOpenProjectDialog);
		CreateProjectCommand = ReactiveCommand.Create(ShowCreateProjectDialog);
		
		FillMockData();
	}

	private void ShowOpenProjectDialog()
	{
		_logger.Log("Show open project dialog");
	}

	private void ShowCreateProjectDialog()
	{
		_logger.Log("Show create project dialog");
	}

	private void FillMockData()
	{
		var project = new Project();
		project.SetProjectName("Project name");
		project.SetProjectPath("Project Path");
		project.SetProjectLastEditTime(DateTime.Now);
		
		AvailableProjects.Add(project);
		AvailableProjects.Add(project);
		AvailableProjects.Add(project);
	}
}