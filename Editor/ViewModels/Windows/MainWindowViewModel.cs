using Editor.Models.Services.Logging;
using Editor.Utils.Factory;

namespace Editor.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
	#region ActiveTab

	private BaseViewModel? _activeTab;
	public BaseViewModel? ActiveTab
	{
		get => _activeTab;
		private set => SetField(ref _activeTab, value);
	}

	#endregion
	
	private readonly ILogger<MainWindowViewModel> _logger;

#pragma warning disable CS8618
	public MainWindowViewModel() { }
#pragma warning restore CS8618

	public MainWindowViewModel(ILogger<MainWindowViewModel> logger)
	{
		_logger = logger;
	}

	public void OpenProjectSelectionWindow(IFactory<ProjectSelectionWindowViewModel> vm)
	{
		_logger.Log("Open project selection window");
		
		ActiveTab = vm.CreateInstance();
	}
}