using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Editor.Models.Services.Logging;
using Editor.Models.Services.Preferences;
using Editor.Utils.Factory;
using Editor.ViewModels;
using Editor.Views;

namespace Editor.Startup;

public class EditorEntryPoint
{
	private readonly ILogger<EditorEntryPoint> _logger;
	
	private readonly IEditorPreferencesService _editorPreferencesService;
	
	private readonly MainWindow _mainWindow;
	private readonly IFactory<MainWindowViewModel> _mainWindowViewModelFactory;
	private readonly IFactory<ProjectManagementWindowViewModel> _projectManagementWindowViewModelFactory;

	public EditorEntryPoint(
		ILogger<EditorEntryPoint> logger,
		IEditorPreferencesService editorPreferencesService,
		MainWindow mainWindow,
		IFactory<MainWindowViewModel> mainWindowViewModelFactory,
		IFactory<ProjectManagementWindowViewModel> projectManagementWindowViewModelFactory)
	{
		_logger = logger;
		_editorPreferencesService = editorPreferencesService;
		_mainWindow = mainWindow;
		_mainWindowViewModelFactory = mainWindowViewModelFactory;
		_projectManagementWindowViewModelFactory = projectManagementWindowViewModelFactory;
	}

	public void Start()
	{
		_logger.LogWarning("Editor started");

		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			try
			{
				await InitializeAsync();
				OpenMainWindow();
			}
			catch (Exception e)
			{
				_logger.LogException(e);
			}
		});
	}

	private async Task InitializeAsync()
	{
		_logger.Log("Initialize");
		
		await _editorPreferencesService.InitializeAsync();
	}

	private void OpenMainWindow()
	{
		_logger.Log("Configure main window");
		
		var mainWindowViewModel = _mainWindowViewModelFactory.CreateInstance();
		_mainWindow.DataContext = mainWindowViewModel;
		
		var projectManagementWindowViewModel = mainWindowViewModel.ActiveTab.Navigate(_projectManagementWindowViewModelFactory);
		projectManagementWindowViewModel.OpenProjectsListTab();
	}
}