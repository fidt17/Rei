using System;

namespace Editor.Models.ProjectManagement;

public class Project
{
	public string ProjectName { get; private set; } = "";
	public string ProjectFilePath { get; private set; } = "";
	public DateTime LastEditTime { get; private set; }

	public void SetProjectName(string value) => ProjectName = value;
	public void SetProjectFilePath(string value) => ProjectFilePath = value;
	public void SetProjectLastEditTime(DateTime value) => LastEditTime = value;
}