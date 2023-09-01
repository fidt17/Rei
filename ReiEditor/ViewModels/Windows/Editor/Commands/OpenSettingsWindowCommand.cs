using System;
using System.Windows.Input;
using ReiEditor.Models.EditorApp.SettingsWindow;

namespace ReiEditor.ViewModels.Windows.Editor.Commands;

public class OpenSettingsWindowCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly ISettingsWindowService _settingsWindowService;

	public OpenSettingsWindowCommand(ISettingsWindowService settingsWindowService)
	{
		_settingsWindowService = settingsWindowService;
		_settingsWindowService.IsOpened.Subscribe(HandleIsOpenedValueChangedEvent);
	}

	public void Dispose()
	{
		_settingsWindowService.IsOpened.Unsubscribe(HandleIsOpenedValueChangedEvent);
	}

	private void HandleIsOpenedValueChangedEvent(bool isOpened)
	{
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}

	public bool CanExecute(object? parameter) => !_settingsWindowService.IsOpened.Value;

	public void Execute(object? parameter)
	{
		_settingsWindowService.OpenSettingsWindow();
	}
}