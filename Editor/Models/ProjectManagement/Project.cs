using System;

namespace Editor.Models.ProjectManagement;

public class Project
{
	public string ProjectName { get; set; } = "";
	public string ProjectFilePath { get; set; } = "";
	public DateTime LastEditTime { get; set; }

	public void SetProjectName(string value) => ProjectName = value;
	public void SetProjectFilePath(string value) => ProjectFilePath = value;
	public void SetProjectLastEditTime(DateTime value) => LastEditTime = value;
}