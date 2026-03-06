using Avalonia.Platform.Storage;

namespace ReiEditor.Models.ProjectManagement.Creation;

public static class ProjectCreationUtils
{
	public static ProjectCreationConfiguration GetDefaultProjectCreationConfiguration(IStorageProvider storageProvider)
	{
		const string DEFAULT_PROJECT_NAME = "New Project";

		var configuration = new ProjectCreationConfiguration
		{
			ProjectName = DEFAULT_PROJECT_NAME,
			ParentDirectoryPath = GetDefaultProjectParentDirectory(storageProvider)
		};

		return configuration;
	}

	private static string GetDefaultProjectParentDirectory(IStorageProvider _)
	{
		return "C:\\Repos\\Rei Projects\\New Project"; // TODO: remove
		
		/*
		var documentsDirectory = storageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents).Result;
		return documentsDirectory == null ? "" : HttpUtility.UrlDecode(documentsDirectory.Path.AbsolutePath);
	*/
	}
}