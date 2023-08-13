using Avalonia.Platform.Storage;

namespace ReiEditor.Models.Services.FileSystem;

public static class FileExtensions
{
	public const string PROJECT_FILE_EXTENSION = ".rei";

	public static FilePickerFileType GetReiProjectFilePickerFileType()
	{
		return new FilePickerFileType(".rei")
		{
			Patterns = new[] { $"*{PROJECT_FILE_EXTENSION}" }
		};
	}
}