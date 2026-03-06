using System;
using System.IO;
using Newtonsoft.Json;

namespace ReiEditor.Models.ProjectManagement;

public class Project
{
	[JsonProperty] public string ProjectName { get; private set; } = "";
	[JsonProperty] public DateTime LastEditTime { get; private set; }
	[JsonProperty] public string ProjectSolutionPath { get; private set; } = "";
	[JsonProperty] public string ProjectVisualStudioProjectPath { get; private set; } = "";
	[JsonProperty] public bool HasBeenSetup { get; private set; }
	[JsonProperty] public string LastSceneId { get; private set; } = "";
	
	[JsonIgnore] 
	public string ProjectFilePath { get; private set; } = "";

	public void SetProjectName(string value) => ProjectName = value;
	public void SetProjectLastEditTime(DateTime value) => LastEditTime = value;
	public void SetProjectFilePath(string value) => ProjectFilePath = Path.GetFullPath(value);
	public void SetProjectSolutionPath(string value) => ProjectSolutionPath = Path.GetFullPath(value);
	public void SetProjectVisualStudioProjectPath(string value) => ProjectVisualStudioProjectPath = Path.GetFullPath(value);
	public void SetHasBeenSetup(bool value) => HasBeenSetup = value;
	public void SetLastScene(string id) => LastSceneId = id;
	
	public string GetDirectoryPath() => Path.GetDirectoryName(ProjectFilePath) ?? throw new Exception("Project directory path is missing");

	public bool Equals(Project other)
	{
		return Path.GetFullPath(ProjectFilePath) == Path.GetFullPath(other.ProjectFilePath);
	}
}