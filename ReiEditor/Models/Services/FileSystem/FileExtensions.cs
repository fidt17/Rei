using Avalonia.Platform.Storage;

namespace ReiEditor.Models.Services.FileSystem;

public static class FileExtensions
{
	public const string VS_SOLUTION_FILE_EXTENSION = ".sln";
	public const string VS_PROJECT_FILE_EXTENSION = ".vcxproj";
	public const string PROJECT_FILE_EXTENSION = ".rei";
	public const string ENGINE_FILE_EXTENSION = ".rei_engine";

	public static FilePickerFileType GetReiProjectFilePickerFileType()
	{
		return new FilePickerFileType(PROJECT_FILE_EXTENSION)
		{
			Patterns = new[] { $"*{PROJECT_FILE_EXTENSION}" }
		};
	}

	public static FilePickerFileType GetReiEngineFilePickerFileType()
	{
		return new FilePickerFileType(ENGINE_FILE_EXTENSION)
		{
			Patterns = new[] { $"*{ENGINE_FILE_EXTENSION}" }
		};
	}
}