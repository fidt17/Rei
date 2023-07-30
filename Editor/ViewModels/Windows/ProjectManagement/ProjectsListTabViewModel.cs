using System;
using System.Collections.ObjectModel;
using Editor.Models.ProjectManagement;
using Editor.Models.Services.Logging;
using Editor.Utils;

namespace Editor.ViewModels;

public class ProjectsListTabViewModel : BaseViewModel
{
	public RelayCommand OpenProjectCommand { get; } = new();
	public RelayCommand CreateProjectCommand { get; } = new();
	
	public ObservableCollection<Project> AvailableProjects { get; } = new();
	
#pragma warning disable CS8618
	public ProjectsListTabViewModel() { FillMockData(); }
#pragma warning restore CS8618

	public ProjectsListTabViewModel(ILogger<ProjectManagementWindowViewModel> logger)
	{
		FillMockData();
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