using Editor.Models.Services.Logging;

namespace Editor.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
	public TabContainer TabContainer { get; } = new();
	
	private readonly ILogger<MainWindowViewModel> _logger;

#pragma warning disable CS8618
	public MainWindowViewModel() { }
#pragma warning restore CS8618

	public MainWindowViewModel(ILogger<MainWindowViewModel> logger)
	{
		_logger = logger;
		
		TabContainer.TabChangedEvent += () =>
		{
			if (TabContainer.ActiveTab != null)
			{
				_logger.Log($"Open tab {TabContainer.ActiveTab.GetType().Name}");
			}
		};
	}
}