using System;
using System.Diagnostics;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.FileSystem;

public class WindowsFileExplorerProvider : IFileExplorerProvider
{
	private readonly ILogger<WindowsFileExplorerProvider> _logger;

	public WindowsFileExplorerProvider(ILogger<WindowsFileExplorerProvider> logger)
	{
		_logger = logger;
	}

	public void OpenDirectory(string directoryPath)
	{
		try
		{
			Process.Start("explorer.exe", $@"{directoryPath}");
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
	}
}