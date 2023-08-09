using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using DynamicData;
using Editor.Models.ProjectManagement;
using Editor.Models.ProjectManagement.BookmarkedProjects;
using Editor.Utils;

namespace Editor.ViewModels;

public class ProjectsListTabViewModel : BaseViewModel
{
	private readonly IBookmarkedProjectsService _bookmarkedProjectsService;
	public RelayCommand OpenProjectCommand { get; } = new();
	public RelayCommand CreateProjectCommand { get; } = new();
	
	public ObservableCollection<Project> AvailableProjects { get; } = new();
	
#pragma warning disable CS8618
	public ProjectsListTabViewModel() { }
#pragma warning restore CS8618

	public ProjectsListTabViewModel(IBookmarkedProjectsService bookmarkedProjectsService)
	{
		_bookmarkedProjectsService = bookmarkedProjectsService;
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
		AvailableProjects.AddRange(projects);
	}
}