using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Editor.Models.ProjectManagement;
using Editor.Models.ProjectManagement.BookmarkedProjects;
using Editor.Models.Services.FileSystem;
using Editor.Models.Services.Logging;
using Editor.Models.Services.Serialization;

namespace Editor.ViewModels.Commands;

public class OpenProjectCommand : BaseViewModel, ICommand
{
	public event EventHandler? CanExecuteChanged;

	private readonly IStorageProvider _storageProvider;
	private readonly ISerializer _serializer;
	private readonly ILogger<OpenProjectCommand> _logger;
	private readonly IBookmarkedProjectsService _bookmarkedProjectsService;

	public OpenProjectCommand(
		IStorageProvider storageProvider, 
		ISerializer serializer, 
		ILogger<OpenProjectCommand> logger, 
		IBookmarkedProjectsService bookmarkedProjectsService)
	{
		_storageProvider = storageProvider;
		_serializer = serializer;
		_logger = logger;
		_bookmarkedProjectsService = bookmarkedProjectsService;
	}

	public bool CanExecute(object? parameter) => true;

	public void Execute(object? parameter)
	{
		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			try
			{
				var path = await GetProjectPath();
				path = HttpUtility.UrlDecode(path);
				if (path == null) return;
				
				var file = await File.ReadAllTextAsync(path);
				var project = _serializer.Deserialize<Project>(file);
				project.SetProjectFilePath(path);
				
				_bookmarkedProjectsService.AddProject(project);
			}
			catch (Exception e)
			{
				_logger.LogException(e);
			}
		});
	}

	private async Task<string?> GetProjectPath()
	{
		var result = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
		{
			Title = "Open Project",
			AllowMultiple = false,
			FileTypeFilter = new [] {FileExtensions.GetReiProjectFilePickerFileType()}
		});

		if (result.Count == 0) return null;

		var projectPath = result[0].Path.AbsolutePath;
		return projectPath;
	}
}