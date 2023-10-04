using System;
using System.Windows.Input;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Views.Windows.Editor.Commands;

public class SaveProjectCommand : ICommand
{
	private readonly IAssetsService _assetsService;

	public SaveProjectCommand(IAssetsService assetsService)
	{
		_assetsService = assetsService;
	}

	public bool CanExecute(object? parameter) => !_assetsService.SaveInProcess.Value;

	public void Execute(object? parameter) => _assetsService.SaveProject();

	public event EventHandler? CanExecuteChanged;
}