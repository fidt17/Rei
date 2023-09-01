using System;
using System.IO;
using System.Linq;

namespace ReiEditor.Models.ProjectManagement.Creation;

public class ProjectCreationValidator
{
	private readonly ProjectCreationConfiguration _configuration;

	public ProjectCreationValidator(ProjectCreationConfiguration configuration)
	{
		_configuration = configuration;
	}

	public bool IsProjectNameValid()
	{
		if (string.IsNullOrWhiteSpace(_configuration.ProjectName)) return false;

		const int MAX_PROJECT_NAME_LENGTH = 31;
		if (_configuration.ProjectName.Length > MAX_PROJECT_NAME_LENGTH) return false;

		var invalidChars = Path.GetInvalidFileNameChars().ToList();
		return _configuration.ProjectName.ToCharArray().All(c => !invalidChars.Contains(c));
	}

	public bool IsProjectPathValid()
	{
		bool IsDirectoryEmpty(string path) => !Directory.EnumerateFileSystemEntries(path).Any();

		try
		{
			Path.GetFullPath(_configuration.FullPath);
			if (Directory.Exists(_configuration.FullPath) && !IsDirectoryEmpty(_configuration.FullPath))
			{
				return false;
			}
		}
		catch (Exception)
		{
			return false;
		}

		return !Directory.Exists(_configuration.FullPath);
	}

	public bool IsConfigurationValid()
	{
		if (!IsProjectNameValid()) return false;
		if (!IsProjectPathValid()) return false;

		return true;
	}
}