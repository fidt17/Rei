using Avalonia.Platform.Storage;

namespace ReiEditor.Models.Services.FileSystem;

public static class FileExtensions
{
	public const string VS_SOLUTION = ".sln";
	public const string VS_PROJECT = ".vcxproj";
	public const string REI_PROJECT = ".rei";
	public const string REI_ENGINE = ".rei_engine";
	public const string DLL = ".dll";

	public static FilePickerFileType GetReiProjectFilePickerFileType()
	{
		return new FilePickerFileType(REI_PROJECT)
		{
			Patterns = new[] { $"*{REI_PROJECT}" }
		};
	}

	public static FilePickerFileType GetReiEngineFilePickerFileType()
	{
		return new FilePickerFileType(REI_ENGINE)
		{
			Patterns = new[] { $"*{REI_ENGINE}" }
		};
	}
}