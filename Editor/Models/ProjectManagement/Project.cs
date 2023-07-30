using System;

namespace Editor.Models.ProjectManagement;

public class Project
{
	public string Name { get; private set; } = "";
	public string Path { get; private set; } = "";
	public DateTime LastEditTime { get; private set; }

	public void SetProjectName(string value) => Name = value;
	public void SetProjectPath(string value) => Path = value;
	public void SetProjectLastEditTime(DateTime value) => LastEditTime = value;
}