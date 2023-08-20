using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows;

public class ShellWindowViewModel : BaseViewModel
{
	public NavigationStore ActiveTab { get; } = new();
	
#pragma warning disable CS8618
	public ShellWindowViewModel() { }
#pragma warning restore CS8618

	public ShellWindowViewModel(ILogger<ShellWindowViewModel> logger)
	{
		ActiveTab.LogOnNavigate(logger);
	}
}