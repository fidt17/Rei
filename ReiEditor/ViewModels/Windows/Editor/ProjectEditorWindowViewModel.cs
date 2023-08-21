using System.Threading.Tasks;
using Avalonia.Threading;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Console;

namespace ReiEditor.ViewModels.Windows.Editor;

public class ProjectEditorWindowViewModel : BaseViewModel
{
	public Project Project { get; }

	public ConsoleEditorWindowViewModel Console { get; } = new();

#pragma warning disable CS8618
	public ProjectEditorWindowViewModel() { }
#pragma warning restore CS8618

	public ProjectEditorWindowViewModel(IActiveProjectService activeProjectService, ConsoleEditorWindowViewModel console,  ILogger<ProjectEditorWindowViewModel> logger)
	{
		Console = console;
		Project = activeProjectService.GetActiveProject();

		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			for (int i = 0; i < 50; i++)
			{
				if (i % 4 == 0)
					logger.Log($"Hello: {i}");
				else if (i % 4 == 1)
					logger.LogWarning($"Hello: {i}");
				else if (i % 4 == 2)
					logger.LogError($"Hello: {i}");

				await Task.Delay(250);
			}
		});
	}

	public override void Dispose()
	{
		base.Dispose();
		Console.Dispose();
	}
}