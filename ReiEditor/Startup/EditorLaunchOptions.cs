using System;
using System.IO;
using Newtonsoft.Json;
using ReiEditor.Models.ProjectManagement;

namespace ReiEditor.Startup;

public static class EditorLaunchOptions
{
    public static string GetStorageDirectory(string? overridePath, string documentsDirectory)
    {
        if (string.IsNullOrWhiteSpace(overridePath)) return Path.Combine(documentsDirectory, "Rei Engine");
        if (!Path.IsPathFullyQualified(overridePath)) throw new ArgumentException("REI_EDITOR_STORAGE must be an absolute path.");
        return Path.GetFullPath(overridePath);
    }

    public static Project? LoadStartupProject(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) return null;
        if (!Path.IsPathFullyQualified(projectPath)) throw new ArgumentException("REI_STARTUP_PROJECT must be an absolute path.");
        var project = JsonConvert.DeserializeObject<Project>(File.ReadAllText(projectPath))
            ?? throw new InvalidDataException("Invalid startup project.");
        project.SetProjectFilePath(projectPath);
        if (!File.Exists(project.ProjectSolutionPath) || !File.Exists(project.ProjectVisualStudioProjectPath))
            throw new InvalidDataException("Startup project solution or C++ project does not exist.");
        return project;
    }
}
