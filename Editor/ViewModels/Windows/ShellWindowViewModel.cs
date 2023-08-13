using Editor.Models.Services.Logging;

namespace Editor.ViewModels;

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