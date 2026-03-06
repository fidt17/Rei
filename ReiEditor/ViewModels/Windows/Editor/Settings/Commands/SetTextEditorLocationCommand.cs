using System;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.ViewModels.Windows.Editor.Settings.Commands;

public class SetTextEditorLocationCommand : ICommand
{
    public event Action<string>? TextEditorPathSetEvent;

    public event EventHandler? CanExecuteChanged;

    private readonly IStorageProvider _storageProvider;
    private readonly IEditorSettingsService _editorSettingsService;
    private readonly ILogger<SetTextEditorLocationCommand> _logger;

    public SetTextEditorLocationCommand(
        IStorageProvider storageProvider,
        IEditorSettingsService editorSettingsService,
        ILogger<SetTextEditorLocationCommand> logger)
    {
        _storageProvider = storageProvider;
        _editorSettingsService = editorSettingsService;
        _logger = logger;
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                var path = await GetPath();
                path = HttpUtility.UrlDecode(path);
                if (path == null) return;

                var isSet = _editorSettingsService.SetTextEditorLocation(path);
                if (isSet)
                {
                    TextEditorPathSetEvent?.Invoke(path);
                }
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        });

        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<string?> GetPath()
    {
        var result = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Text Editor",
            AllowMultiple = false,
            FileTypeFilter = new[] { FileExtensions.GetFilePicker(FileExtensions.EXE) }
        });

        if (result.Count == 0) return null;

        return result[0].Path.AbsolutePath;
    }
}
