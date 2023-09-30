using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Settings;
using ReiEditor.Views.Windows.Editor.Settings;

namespace ReiEditor.Models.EditorApp.SettingsWindow;

public class SettingsWindowService : ISettingsWindowService
{
	public IObservable<bool> IsOpened => _isOpened;
	private readonly Observable<bool> _isOpened = new(false);

	private EditorSettingsWindowView? _window;

	private readonly IFactory<EditorSettingsWindowViewModel> _vmFactory;
	private readonly ILogger<SettingsWindowService> _logger;
	private readonly IMainWindowService _mainWindowService;

	public SettingsWindowService(IFactory<EditorSettingsWindowViewModel> vmFactory, ILogger<SettingsWindowService> logger, IMainWindowService mainWindowService)
	{
		_vmFactory = vmFactory;
		_logger = logger;
		_mainWindowService = mainWindowService;
	}

	public void OpenSettingsWindow()
	{
		if (_isOpened)
		{
			_logger.LogError("Cannot open settings window since it is already active");
			return;
		}

		var vm = _vmFactory.CreateInstance();
		_window = new EditorSettingsWindowView
		{
			DataContext = vm
		};
		_mainWindowService.ShowDialog(_window);
		_isOpened.Value = true;

		_window.Closed += (_, _) =>
		{
			_isOpened.Value = false;
			vm.Dispose();
		};
	}

	public void CloseSettingsWindow()
	{
		if (_window == null)
		{
			_logger.LogError("Cannot close settings window since it is not opened.");
			return;
		}
		
		_window.Close();
	}
}