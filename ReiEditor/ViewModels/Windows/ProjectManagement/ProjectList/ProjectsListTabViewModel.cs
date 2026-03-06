using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.BookmarkedProjects;
using ReiEditor.Utils;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.ProjectManagement.Commands;

namespace ReiEditor.ViewModels.Windows.ProjectManagement;

public class ProjectsListTabViewModel : BaseViewModel
{
	public OpenProjectCommand OpenProjectCommand { get; }
	public RelayCommand CreateProjectCommand { get; } = new();
	
	public ObservableCollection<ProjectsListElementViewModel> AvailableProjects { get; } = new();
	
	private readonly IBookmarkedProjectsService _bookmarkedProjectsService;
	private readonly IFactory<ProjectsListElementViewModel> _projectListElementFactory;

#pragma warning disable CS8618
	public ProjectsListTabViewModel() { }
#pragma warning restore CS8618

	public ProjectsListTabViewModel(
		IBookmarkedProjectsService bookmarkedProjectsService, 
		IFactory<ProjectsListElementViewModel> projectListElementFactory, 
		IFactory<OpenProjectCommand> openProjectCommandFactory)
	{
		_bookmarkedProjectsService = bookmarkedProjectsService;
		_projectListElementFactory = projectListElementFactory;

		OpenProjectCommand = openProjectCommandFactory.CreateInstance();
		
		_bookmarkedProjectsService.BookmarkedProjectsCollectionChangedEvent += HandleBookmarkedProjectsCollectionChangedEvent;
		UpdateAvailableProjects(_bookmarkedProjectsService.GetBookmarkedProjects());
	}

	public override void Dispose()
	{
		base.Dispose();
		_bookmarkedProjectsService.BookmarkedProjectsCollectionChangedEvent -= HandleBookmarkedProjectsCollectionChangedEvent;
	}

	private void HandleBookmarkedProjectsCollectionChangedEvent()
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			UpdateAvailableProjects(_bookmarkedProjectsService.GetBookmarkedProjects());
		});
	}

	private void UpdateAvailableProjects(IEnumerable<Project> projects)
	{
		foreach (var projectsListElementViewModel in AvailableProjects)
		{
			projectsListElementViewModel.Dispose();
		}
		AvailableProjects.Clear();
		
		foreach (var project in projects)
		{
			var vm = _projectListElementFactory.CreateInstance(project);
			AvailableProjects.Add(vm);
		}
	}
}