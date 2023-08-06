using System.Web;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Editor.Models.ProjectManagement.Creation;
using Editor.Utils;
using ReactiveUI;

namespace Editor.ViewModels;

public class ProjectCreationTabViewModel : BaseViewModel
{
	public RelayCommand CancelProjectCreationCommand { get; } = new();
	public ICommand SelectProjectLocationCommand { get; }
	public CreateProjectCommand CreateProjectCommand { get; }
	
	#region ProjectName

	private string _projectName = "";
	public string ProjectName
	{
		get => _projectName;
		set
		{
			if (!SetField(ref _projectName, value)) return;
			_projectCreationService.Configuration.ProjectName = ProjectName;
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

	public ProjectCreationTabNotifications Notifications { get; }
	
	private readonly IStorageProvider _storageProvider;
	private readonly IProjectCreationService _projectCreationService;

#pragma warning disable CS8618
	public ProjectCreationTabViewModel() { }
#pragma warning restore CS8618

	public ProjectCreationTabViewModel(IStorageProvider storageProvider, IProjectCreationService projectCreationService)
	{
		_storageProvider = storageProvider;
		_projectCreationService = projectCreationService;

		Notifications = new ProjectCreationTabNotifications(_projectCreationService);
		
		SelectProjectLocationCommand = ReactiveCommand.Create(ExecuteSelectProjectLocationCommand);
		CreateProjectCommand = new CreateProjectCommand(projectCreationService);

		_projectCreationService.Configuration.ConfigurationChangedEvent += UpdateConfigurationValues;
		UpdateConfigurationValues();
	}

	public override void Dispose()
	{
		base.Dispose();
		Notifications.Dispose();
		CreateProjectCommand.Dispose();
	}

	private void UpdateConfigurationValues()
	{
		ProjectName = _projectCreationService.Configuration.ProjectName;
		ProjectPath = _projectCreationService.Configuration.FullPath;
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
			
			var path = HttpUtility.UrlDecode(selectedFolder[0].Path.AbsolutePath);
			_projectCreationService.Configuration.ParentDirectoryPath = path;
		});
	}
}