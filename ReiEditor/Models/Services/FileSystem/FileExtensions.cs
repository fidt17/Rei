using Avalonia.Platform.Storage;

namespace ReiEditor.Models.Services.FileSystem;

public static class FileExtensions
{
	public const string VS_SOLUTION = ".sln";
	public const string VS_PROJECT = ".vcxproj";
	public const string EXE = ".exe";
	
	public const string REI_PROJECT = ".rei";
	public const string REI_ENGINE = ".rei_engine";
	
	public const string SCENE = ".scene";
	public const string ASSET = ".asset";
	public const string META = ".meta";

	public static FilePickerFileType GetFilePicker(string fileExtension)
	{
		return new FilePickerFileType(fileExtension)
		{
			Patterns = new[] { $"*{fileExtension}" }
		};
	}
}