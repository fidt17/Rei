using System;
using System.Diagnostics;
using System.IO;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.FileSystem;

public class WindowsTextEditorFileOpener : ITextEditorFileOpener
{
    private readonly IEditorSettingsService _editorSettingsService;
    private readonly ILogger<WindowsTextEditorFileOpener> _logger;

    public WindowsTextEditorFileOpener(
        IEditorSettingsService editorSettingsService,
        ILogger<WindowsTextEditorFileOpener> logger)
    {
        _editorSettingsService = editorSettingsService;
        _logger = logger;
    }

    public bool CanOpenWithTextEditor(string filePath) => FileExtensions.IsTextEditorOpenSupported(filePath);

    public TextEditorOpenResult Open(string filePath)
    {
        if (!CanOpenWithTextEditor(filePath)) return TextEditorOpenResult.UnsupportedExtension;
        if (!File.Exists(filePath)) return TextEditorOpenResult.Failed;

        var customEditorPath = _editorSettingsService.GetTextEditorLocation();
        if (!string.IsNullOrWhiteSpace(customEditorPath))
        {
            if (!_editorSettingsService.IsTextEditorLocationValid())
            {
                return TextEditorOpenResult.InvalidCustomEditorPath;
            }

            return OpenWithCustomEditor(customEditorPath, filePath);
        }

        return OpenWithShellDefault(filePath);
    }

    private TextEditorOpenResult OpenWithCustomEditor(string editorPath, string filePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = editorPath,
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false
            };

            var process = Process.Start(startInfo);
            return process == null ? TextEditorOpenResult.Failed : TextEditorOpenResult.Opened;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return TextEditorOpenResult.Failed;
        }
    }

    private TextEditorOpenResult OpenWithShellDefault(string filePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            return process == null ? TextEditorOpenResult.Failed : TextEditorOpenResult.Opened;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return TextEditorOpenResult.Failed;
        }
    }
}
