using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Storage;

public class EditorStorageService : IEditorStorageService
{
	private const string STORAGE_DIRECTORY_NAME = "Rei Engine";
	
	private readonly ILogger<EditorStorageService> _logger;
	private readonly string _appStorageDirectory;

	public EditorStorageService(ILogger<EditorStorageService> logger)
	{
		_logger = logger;
		
		_appStorageDirectory = GetAppStorageDirectory();
	}

	public async Task<bool> WriteToFile(string fileName, string value)
	{
		try
		{
			var path = Path.Combine(_appStorageDirectory, fileName);
			await File.WriteAllTextAsync(path, value);
			
			return true;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return false;
	}

	public async Task<string?> ReadFromFile(string fileName)
	{
		try
		{
			var path = Path.Combine(_appStorageDirectory, fileName);
			return await File.ReadAllTextAsync(path);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return null;
	}

	private string GetAppStorageDirectory()
	{
		try
		{
			var documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			var appStoragePath = Path.Combine(documentsDir, STORAGE_DIRECTORY_NAME);
			
			Directory.CreateDirectory(appStoragePath);

			return appStoragePath;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
			throw;
		}
	}
}