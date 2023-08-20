using System;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging;

namespace ReiEditor.ViewModels.Windows.ProjectManagement.Commands;

public class SetEngineLocationCommand : ICommand
{
	public event Action<string>? EnginePathSetEvent;
	
	public event EventHandler? CanExecuteChanged;

	private readonly IStorageProvider _storageProvider;
	private readonly IEditorConfigurationService _editorConfigurationService;
	private readonly ILogger<SetEngineLocationCommand> _logger;

	public SetEngineLocationCommand(IStorageProvider storageProvider, IEditorConfigurationService editorConfigurationService, ILogger<SetEngineLocationCommand> logger)
	{
		_storageProvider = storageProvider;
		_editorConfigurationService = editorConfigurationService;
		_logger = logger;
	}

	public bool CanExecute(object? parameter) => true;

	public void Execute(object? parameter)
	{
		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			try
			{
				var path = await GetEnginePath();
				path = HttpUtility.UrlDecode(path);
				if (path == null) return;

				var isSet = _editorConfigurationService.SetEngineLocation(path);

				if (isSet)
				{
					EnginePathSetEvent?.Invoke(path);
				}
			}
			catch (Exception e)
			{
				_logger.LogException(e);
			}
		});
	}

	private async Task<string?> GetEnginePath()
	{
		var result = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
		{
			Title = "Select Rei Engine",
			AllowMultiple = false,
			FileTypeFilter = new [] {FileExtensions.GetReiEngineFilePickerFileType()}
		});
        
		if (result.Count == 0) return null;
        
		var projectPath = result[0].Path.AbsolutePath;
		return projectPath;
	}
}