using System.Web;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Editor.Utils;
using ReactiveUI;

namespace Editor.ViewModels;

public class ProjectCreationTabViewModel : BaseViewModel
{
	public RelayCommand CancelProjectCreationCommand { get; } = new();
	public ICommand SelectProjectLocationCommand { get; }
	
	#region ProjectName

	private string _projectName = "New Project";
	public string ProjectName
	{
		get => _projectName;
		set
		{
			if (SetField(ref _projectName, value))
			{
				UpdateProjectPath(ProjectName);
			}
		}
	}

	#endregion
	
	#region ProjectPath

	private string _projectPath = "";
	public string ProjectPath
	{
		get => _projectPath;
		private set => SetField(ref _projectPath, value);
	}

	#endregion

	private string? _selectedParentDir;
	
	private readonly IStorageProvider _storageProvider;

	public ProjectCreationTabViewModel()
	{
	}

	public ProjectCreationTabViewModel(IStorageProvider storageProvider)
	{
		_storageProvider = storageProvider;
		SelectProjectLocationCommand = ReactiveCommand.Create(ExecuteSelectProjectLocationCommand);

		ProjectName = GetDefaultProjectName();
		_selectedParentDir = GetDefaultPath();
		UpdateProjectPath(ProjectName);
	}

	private void ExecuteSelectProjectLocationCommand()
	{
		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			var selectedFolder = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
			{
				Title = "Select project location",
				AllowMultiple = false,
			});
			
			if (selectedFolder.Count == 0) return;
			
			var path = selectedFolder[0].Path.AbsolutePath;
			if (string.IsNullOrEmpty(path)) return;
			if (string.IsNullOrEmpty(ProjectName)) return;

			_selectedParentDir = path;
			UpdateProjectPath(ProjectName);
		});
	}

	private void UpdateProjectPath(string projectName)
	{
		if (string.IsNullOrEmpty(projectName)) return;
		ProjectPath = _selectedParentDir + $"/{projectName}";
	}

	private string GetDefaultProjectName() => "New Project";

	private string GetDefaultPath()
	{
		var documentsDir = _storageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents).Result?.Path.AbsolutePath;
		return string.IsNullOrEmpty(documentsDir) ? "" : HttpUtility.UrlDecode(documentsDir + "/ReiProjects");
	}
}