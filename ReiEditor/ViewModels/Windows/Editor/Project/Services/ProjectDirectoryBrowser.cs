using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using IOPath = System.IO.Path;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Project.Directories;
using ReiEditor.ViewModels.Windows.Editor.Project.Path;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Services;

public class ProjectDirectoryBrowser
{
    public ObservableCollection<ProjectDirectoryNodeViewModel> RootDirectories { get; } = new();
    public ObservableCollection<ProjectPathSegmentViewModel> PathSegments { get; } = new();
    public ObservableField<string> ActiveDirectoryPath { get; } = new("");

    public string ProjectRootPath => _projectRootPath;
    public string? SelectedDirectoryPath => _selectedDirectory?.FullPath;

    private readonly List<ProjectDirectoryNodeViewModel> _allNodes = new();
    private readonly Dictionary<string, ProjectDirectoryNodeViewModel> _nodeByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<bool> _hasSearchQuery;
    private readonly Action _resetSearch;
    private readonly Action<string> _directorySelectedAction;

    private ProjectDirectoryNodeViewModel? _selectedDirectory;
    private string _projectRootPath = "";

    public ProjectDirectoryBrowser(
        Func<bool> hasSearchQuery,
        Action resetSearch,
        Action<string> directorySelectedAction)
    {
        _hasSearchQuery = hasSearchQuery;
        _resetSearch = resetSearch;
        _directorySelectedAction = directorySelectedAction;
    }

    public void BuildTree(IResourceService resourceService, string? preferredPath = null)
    {
        Reset();

        var rootPath = resourceService.GetProjectPath();
        _projectRootPath = rootPath;
        if (!Directory.Exists(rootPath)) return;

        var rootNode = CreateDirectoryNode(rootPath, isRoot: true);
        RootDirectories.Add(rootNode);

        if (!string.IsNullOrEmpty(preferredPath) && _nodeByPath.TryGetValue(preferredPath, out var preferredNode))
        {
            SelectDirectory(preferredNode);
            return;
        }

        SelectDirectory(rootNode);
    }

    public void OpenDirectory(string fullPath)
    {
        if (!_nodeByPath.TryGetValue(fullPath, out var node)) return;

        ExpandToNode(node);
        SelectDirectory(node);
    }

    public void Reset()
    {
        RootDirectories.ClearAndDispose();
        PathSegments.Clear();
        _allNodes.Clear();
        _nodeByPath.Clear();
        _selectedDirectory = null;
        _projectRootPath = "";
        ActiveDirectoryPath.Value = "";
    }

    private ProjectDirectoryNodeViewModel CreateDirectoryNode(
        string fullPath,
        bool isRoot,
        ProjectDirectoryNodeViewModel? parent = null)
    {
        var name = isRoot ? "Project" : IOPath.GetFileName(fullPath);
        var node = new ProjectDirectoryNodeViewModel(name, fullPath, parent);
        if (isRoot)
        {
            node.Expanded.Value = true;
        }

        RegisterNode(node);

        foreach (var directory in Directory.EnumerateDirectories(fullPath).OrderBy(IOPath.GetFileName))
        {
            var childNode = CreateDirectoryNode(directory, isRoot: false, parent: node);
            node.ChildNodes.Add(childNode);
        }

        return node;
    }

    private void RegisterNode(ProjectDirectoryNodeViewModel node)
    {
        _allNodes.Add(node);
        _nodeByPath[node.FullPath] = node;
        node.Selected.ChangedEvent += _ => HandleNodeSelectedChangedEvent(node);
    }

    private void HandleNodeSelectedChangedEvent(ProjectDirectoryNodeViewModel node)
    {
        if (!node.Selected.Value) return;

        SelectDirectory(node);
    }

    private void SelectDirectory(ProjectDirectoryNodeViewModel node)
    {
        if (_hasSearchQuery())
        {
            _resetSearch();
        }

        _selectedDirectory = node;
        ActiveDirectoryPath.Value = node.FullPath;
        UpdatePathSegments(node.FullPath);
        _directorySelectedAction(node.FullPath);

        foreach (var other in _allNodes)
        {
            if (other == node) continue;
            other.Deselect();
        }
    }

    private void UpdatePathSegments(string fullPath)
    {
        PathSegments.Clear();
        if (string.IsNullOrWhiteSpace(_projectRootPath)) return;

        var segments = new List<(string name, string path)>
        {
            ("Project", _projectRootPath)
        };

        var relativePath = IOPath.GetRelativePath(_projectRootPath, fullPath);
        if (relativePath != ".")
        {
            var current = _projectRootPath;
            var split = relativePath.Split(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
            foreach (var segment in split)
            {
                if (string.IsNullOrWhiteSpace(segment)) continue;

                current = IOPath.Combine(current, segment);
                segments.Add((segment, current));
            }
        }

        for (var i = 0; i < segments.Count; i++)
        {
            var separator = i == 0 ? "" : "/ ";
            var item = new ProjectPathSegmentViewModel(segments[i].name, segments[i].path, separator, HandlePathSegmentNavigate);
            PathSegments.Add(item);
        }
    }

    private void HandlePathSegmentNavigate(ProjectPathSegmentViewModel segment)
    {
        OpenDirectory(segment.FullPath);
    }

    private void ExpandToNode(ProjectDirectoryNodeViewModel node)
    {
        var current = node.Parent;
        while (current != null)
        {
            current.Expanded.Value = true;
            current = current.Parent;
        }
    }
}
