using Editor.Models.Services.Logging;

namespace Editor.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
	public TabContainer TabContainer { get; } = new();
	
#pragma warning disable CS8618
	public MainWindowViewModel() { }
#pragma warning restore CS8618

	public MainWindowViewModel(ILogger<MainWindowViewModel> logger)
	{
		TabContainer.LogOnTabChange(logger);
	}
}