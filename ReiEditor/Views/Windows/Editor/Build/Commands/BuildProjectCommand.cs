using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Build;

namespace ReiEditor.Views.Windows.Editor.Build.Commands;

public class BuildProjectCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly IBuildService _buildService;

	public BuildProjectCommand(IBuildService buildService)
	{
		_buildService = buildService;
		_buildService.CanStartBuildChangedEvent += HandleCanStartBuildChangedEvent;
	}

	public void Dispose()
	{
		_buildService.CanStartBuildChangedEvent -= HandleCanStartBuildChangedEvent;
	}
	
	public bool CanExecute(object? parameter) => _buildService.CanStartBuild;

	public void Execute(object? parameter)
	{
		Task.Run(() => _buildService.BuildProject(BuildConfigurationEnum.EditorDebug));
	}
	
	private void HandleCanStartBuildChangedEvent(bool value)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		});
	}
}