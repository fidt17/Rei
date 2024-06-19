using System.Windows.Input;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.ProjectManagement.BookmarkedProjects;
using ReiEditor.Models.ProjectManagement.Deletion;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.ProjectManagement;

public class ProjectsListElementViewModel : BaseViewModel
{
	public ICommand OpenProjectCommand { get; }
	
	public Project Project { get; }
	public ContextMenuViewModel ContextMenu { get; } = new();

	private readonly IFileExplorerProvider _fileExplorerProvider;
	private readonly IProjectDeletionService _projectDeletionService;
	private readonly IBookmarkedProjectsService _bookmarkedProjectsService;

#pragma warning disable CS8618
	public ProjectsListElementViewModel() { }
#pragma warning restore CS8618

	public ProjectsListElementViewModel(Project project, 
		IFileExplorerProvider fileExplorerProvider, 
		IProjectDeletionService projectDeletionService,
		IBookmarkedProjectsService bookmarkedProjectsService,
		IActiveProjectService activeProjectService)
	{
		_fileExplorerProvider = fileExplorerProvider;
		_projectDeletionService = projectDeletionService;
		_bookmarkedProjectsService = bookmarkedProjectsService;
		
		Project = project;
		ContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Reveal in File Explorer", RevealInFileExplorer));
		ContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Delete Project", DeleteProject));

		OpenProjectCommand = new RelayCommand(() => activeProjectService.OpenProject(Project));

		// TODO: remove
		OpenProjectCommand.Execute(null);
	}

	private void RevealInFileExplorer() => _fileExplorerProvider.OpenDirectory(Project.GetDirectoryPath());

	private void DeleteProject()
	{
		var title = "Project Deletion";
		var text = $"Are you sure you want to delete {Project.ProjectName}?";
		var confirmDialog = MessageBoxManager.GetMessageBoxStandard(title, text, ButtonEnum.YesNo);

		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			var result = await confirmDialog.ShowAsync();

			if (result == ButtonResult.Yes)
			{
				_projectDeletionService.DeleteProject(Project);
				_bookmarkedProjectsService.RemoveProject(Project);
			}
		});
	}
}