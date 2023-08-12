using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using Editor.Models.ProjectManagement;
using Editor.Models.ProjectManagement.BookmarkedProjects;
using Editor.Utils;
using Editor.Utils.Factory;
using Editor.ViewModels.Commands;

namespace Editor.ViewModels;

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