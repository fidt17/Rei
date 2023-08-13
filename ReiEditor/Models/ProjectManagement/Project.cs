using System;
using System.IO;
using Newtonsoft.Json;

namespace ReiEditor.Models.ProjectManagement;

public class Project
{
	public string ProjectName { get; set; } = "";
	public DateTime LastEditTime { get; set; }
	
	[JsonIgnore] 
	public string ProjectFilePath { get; private set; } = "";

	public void SetProjectName(string value) => ProjectName = value;
	public void SetProjectLastEditTime(DateTime value) => LastEditTime = value;
	
	public void SetProjectFilePath(string value) => ProjectFilePath = Path.GetFullPath(value);
	public string GetDirectoryPath() => Path.GetDirectoryName(ProjectFilePath) ?? throw new Exception("Project directory path is missing");

	public bool Equals(Project other)
	{
		return Path.GetFullPath(ProjectFilePath) == Path.GetFullPath(other.ProjectFilePath);
	}
}