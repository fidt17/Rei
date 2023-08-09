using System;
using System.IO;

namespace Editor.Models.ProjectManagement.Creation;

public class ProjectCreationConfiguration
{
	public event Action? ConfigurationChangedEvent;
	
	private string _projectName = "";
	public string ProjectName
	{
		get => _projectName;
		set
		{
			if (value == _projectName) return;
			
			_projectName = value;
			UpdateFullPath();
			
			ConfigurationChangedEvent?.Invoke();
		}
	}

	private string _parentDirectoryPath = "";
	public string ParentDirectoryPath
	{
		get => _parentDirectoryPath;
		set
		{
			if (value == _parentDirectoryPath) return;
			
			_parentDirectoryPath = value;
			UpdateFullPath();
			
			ConfigurationChangedEvent?.Invoke();
		}
	}

	public string FullPath { get; private set; } = "";

	private void UpdateFullPath()
	{
		FullPath = Path.GetFullPath(Path.Combine(_parentDirectoryPath, ProjectName));
		ConfigurationChangedEvent?.Invoke();
	}
}