using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using IOPath = System.IO.Path;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;
using ReiEditor.ViewModels.Windows.Editor.Project.Directories;
using ReiEditor.ViewModels.Windows.Editor.Project.Path;

namespace ReiEditor.ViewModels.Windows.Editor.Project;

public class ProjectWindowViewModel : BaseViewModel
{
    public ObservableCollection<ProjectDirectoryNodeViewModel> RootDirectories { get; } = new();
    public ObservableCollection<ProjectAssetItemViewModel> ActiveItems { get; } = new();
    public ObservableCollection<ProjectPathSegmentViewModel> PathSegments { get; } = new();
    public ObservableField<string> ActiveDirectoryPath { get; } = new("");
    public SearchFieldViewModel SearchField { get; } = new();

    public ContextMenuViewModel ActiveFolderContextMenu { get; } = new();

    private readonly List<ProjectDirectoryNodeViewModel> _allNodes = new();
    private readonly Dictionary<string, ProjectDirectoryNodeViewModel> _nodeByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProjectAssetItemViewModel> _allAssets = new();
    private ProjectDirectoryNodeViewModel? _selectedDirectory;
    private ProjectAssetItemViewModel? _selectedAsset;
    private readonly IResourceService? _resourceService;
    private readonly IStorageProvider? _storageProvider;
    private readonly IAssetOperationsService? _assetOperationsService;
    private readonly IFileExplorerProvider? _fileExplorerProvider;
    private readonly IAssetSearchService? _assetSearchService;
    private string _projectRootPath = "";
    private string _pendingSearchSelectionPath = "";

#pragma warning disable CS8618
    public ProjectWindowViewModel()
    {
        SetupContextMenus();
    }
#pragma warning restore CS8618

    public ProjectWindowViewModel(
        IResourceService resourceService,
        IStorageProvider storageProvider,
        IAssetOperationsService assetOperationsService,
        IFileExplorerProvider fileExplorerProvider,
        IAssetSearchService assetSearchService)
    {
        _resourceService = resourceService;
        _storageProvider = storageProvider;
        _assetOperationsService = assetOperationsService;
        _fileExplorerProvider = fileExplorerProvider;
        _assetSearchService = assetSearchService;
        
        SetupContextMenus();
        BuildDirectoryTree(resourceService);
        SearchField.Query.ChangedEvent += HandleSearchQueryChanged;
    }

    public override void Dispose()
    {
        base.Dispose();
        SearchField.Query.ChangedEvent -= HandleSearchQueryChanged;
        ResetDirectoryTree();
    }

    private void SetupContextMenus()
    {
        ActiveFolderContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Show in Explorer", OpenActiveFolderInExplorer));
        ActiveFolderContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Create Folder", CreateFolder));
    }

    private void OpenActiveFolderInExplorer()
    {
        if (_fileExplorerProvider == null) return;
        
        var activePath = ActiveDirectoryPath.Value;
        if (string.IsNullOrWhiteSpace(activePath)) return;
        
        _fileExplorerProvider.OpenDirectory(activePath);
    }

    private void BuildDirectoryTree(IResourceService resourceService, string? preferredPath = null)
    {
        ResetDirectoryTree();

        var rootPath = resourceService.GetProjectPath();
        _projectRootPath = rootPath;
        if (!Directory.Exists(rootPath)) return;

        var rootNode = CreateDirectoryNode(rootPath, isRoot: true);
        RootDirectories.Add(rootNode);

        if (!string.IsNullOrEmpty(preferredPath) && _nodeByPath.TryGetValue(preferredPath, out var preferredNode))
        {
            SelectDirectory(preferredNode);
        }
        else
        {
            SelectDirectory(rootNode);
        }
    }

    private ProjectDirectoryNodeViewModel CreateDirectoryNode(string fullPath, bool isRoot, ProjectDirectoryNodeViewModel? parent = null)
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
        if (SearchField.HasQuery.Value)
        {
            SearchField.ResetSearch();
        }

        _selectedDirectory = node;
        ActiveDirectoryPath.Value = node.FullPath;
        UpdatePathSegments(node.FullPath);
        UpdateActiveItems(node.FullPath);

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

        var segments = new List<(string name, string path)>();
        segments.Add(("Project", _projectRootPath));

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

    private void OpenDirectory(string fullPath)
    {
        if (!_nodeByPath.TryGetValue(fullPath, out var node)) return;
        ExpandToNode(node);
        SelectDirectory(node);
    }

    private void UpdateActiveItems(string directoryPath)
    {
        ActiveItems.ClearAndDispose();
        _allAssets.Clear();
        _selectedAsset = null;

        if (!Directory.Exists(directoryPath)) return;

        var query = SearchField.Query.Value;
        if (!string.IsNullOrWhiteSpace(query))
        {
            ApplySearchResults(query);
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(directoryPath).OrderBy(IOPath.GetFileName))
        {
            var name = IOPath.GetFileName(directory);
            var item = new ProjectAssetItemViewModel(name, directory, ProjectAssetType.Directory, DeleteAsset, DuplicateAsset, RenameAsset, MoveAsset, OpenAsset, _fileExplorerProvider!);
            RegisterAsset(item);
            ActiveItems.Add(item);
        }

        foreach (var file in Directory.EnumerateFiles(directoryPath).OrderBy(IOPath.GetFileName))
        {
            if (AssetFileFilter.ShouldHide(file)) continue;

            var name = IOPath.GetFileName(file);
            var assetType = GetAssetType(file);
            var item = new ProjectAssetItemViewModel(name, file, assetType, DeleteAsset, DuplicateAsset, RenameAsset, MoveAsset, OpenAsset, _fileExplorerProvider!);
            RegisterAsset(item);
            ActiveItems.Add(item);
        }
    }

    private ProjectAssetType GetAssetType(string filePath)
    {
        var extension = IOPath.GetExtension(filePath);
        if (extension == FileExtensions.SCENE) return ProjectAssetType.Scene;
        if (extension is FileExtensions.H or FileExtensions.CPP) return ProjectAssetType.Script;
        
        return ProjectAssetType.Asset;
    }

    private void RegisterAsset(ProjectAssetItemViewModel item)
    {
        _allAssets.Add(item);
        item.Selected.ChangedEvent += _ => HandleAssetSelectedChangedEvent(item);
    }

    private void HandleAssetSelectedChangedEvent(ProjectAssetItemViewModel item)
    {
        if (!item.Selected.Value) return;
        SelectAsset(item);
    }

    private void SelectAsset(ProjectAssetItemViewModel item)
    {
        _selectedAsset = item;
        TrackSearchSelection(item);

        foreach (var other in _allAssets)
        {
            if (other == item) continue;
            other.Deselect();
        }
    }

    private void OpenAsset(ProjectAssetItemViewModel item)
    {
        if (!item.IsDirectory) return;
        OpenDirectory(item.FullPath);
    }

    private void RenameAsset(ProjectAssetItemViewModel item, string newName)
    {
        if (_assetOperationsService == null) return;

        _ = _assetOperationsService.RenameAsync(item.FullPath, newName).ContinueWith(_ =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshView(affectsTree: item.IsDirectory);
                SelectAssetByPath(IOPath.Combine(IOPath.GetDirectoryName(item.FullPath) ?? "", newName.Trim()));
            });
        });
    }

    private void DeleteAsset(ProjectAssetItemViewModel item)
    {
        if (_assetOperationsService == null) return;

        _ = _assetOperationsService.DeleteAsync(item.FullPath, item.IsDirectory).ContinueWith(_ =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshView(affectsTree: item.IsDirectory);
            });
        });
    }

    private void MoveAsset(ProjectAssetItemViewModel item)
    {
        if (_storageProvider == null) return;
        if (string.IsNullOrWhiteSpace(_projectRootPath)) return;
        if (_assetOperationsService == null) return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var selectedFolder = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Move to Directory",
                AllowMultiple = false
            });

            if (selectedFolder.Count == 0) return;
            var destFolderPath = selectedFolder[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(destFolderPath)) return;

            var fullDestFolderPath = IOPath.GetFullPath(destFolderPath);
            var fullRootPath = IOPath.GetFullPath(_projectRootPath);
            if (!fullDestFolderPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase)) return;

            await _assetOperationsService.MoveAsync(item.FullPath, fullDestFolderPath);
            RefreshView(affectsTree: item.IsDirectory);
            SelectAssetByPath(IOPath.Combine(fullDestFolderPath, IOPath.GetFileName(item.FullPath)));
        });
    }

    private void DuplicateAsset(ProjectAssetItemViewModel item)
    {
        if (_assetOperationsService == null) return;

        _ = _assetOperationsService.DuplicateAsync(item.FullPath, item.IsDirectory).ContinueWith(_ =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshView(affectsTree: item.IsDirectory);
            });
        });
    }

    private void CreateFolder()
    {
        var baseDirectory = ActiveDirectoryPath.Value;
        if (_assetOperationsService == null) return;
        
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory)) return;

        _ = _assetOperationsService.CreateFolderAsync(baseDirectory, "New Folder").ContinueWith(_ =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshView(affectsTree: true);
            });
        });
    }

    public async Task ImportExternalPathsAsync(IReadOnlyCollection<string> paths)
    {
        var targetDirectory = ActiveDirectoryPath.Value;
        
        if (_assetOperationsService == null) return;
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory)) return;
        if (paths.Count == 0) return;

        await _assetOperationsService.ImportExternalAssets(paths, targetDirectory);
        RefreshView(affectsTree: true);
    }

    public void ClearAssetSelection()
    {
        _selectedAsset = null;
        foreach (var asset in _allAssets)
        {
            asset.Deselect();
        }
    }

    private void RefreshView(bool affectsTree)
    {
        var activePath = _selectedDirectory?.FullPath ?? ActiveDirectoryPath.Value;
        if (affectsTree && _resourceService != null)
        {
            BuildDirectoryTree(_resourceService, activePath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(activePath))
        {
            UpdateActiveItems(activePath);
        }
    }

    private void HandleSearchQueryChanged(string query)
    {
        if (SearchField.ShouldSuppressQueryRefresh()) return;
        if (string.IsNullOrWhiteSpace(query))
        {
            if (!string.IsNullOrWhiteSpace(_pendingSearchSelectionPath))
            {
                NavigateToSearchSelection(_pendingSearchSelectionPath);
                _pendingSearchSelectionPath = "";
                return;
            }
        }
        else
        {
            _pendingSearchSelectionPath = "";
        }

        if (string.IsNullOrWhiteSpace(ActiveDirectoryPath.Value)) return;
        
        UpdateActiveItems(ActiveDirectoryPath.Value);
    }

    private void ApplySearchResults(string query)
    {
        if (_assetSearchService == null) return;

        var results = _assetSearchService.Search(query);
        foreach (var result in results)
        {
            var assetType = result.IsDirectory ? ProjectAssetType.Directory : GetAssetType(result.FullPath);
            var item = new ProjectAssetItemViewModel(result.Name, result.FullPath, assetType, DeleteAsset, DuplicateAsset, RenameAsset, MoveAsset, OpenAsset, _fileExplorerProvider!);
            RegisterAsset(item);
            ActiveItems.Add(item);
        }
    }

    private void TrackSearchSelection(ProjectAssetItemViewModel item)
    {
        if (!SearchField.HasQuery.Value) return;
        _pendingSearchSelectionPath = item.FullPath;
    }

    private void NavigateToSearchSelection(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return;

        var targetDirectory = IOPath.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(targetDirectory)) return;

        OpenDirectory(targetDirectory);
        SelectAssetByPath(targetPath);
    }

    private void SelectAssetByPath(string path)
    {
        var match = ActiveItems.FirstOrDefault(item => string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;
        match.Select();
    }

    private void ResetDirectoryTree()
    {
        RootDirectories.ClearAndDispose();
        ActiveItems.ClearAndDispose();
        PathSegments.Clear();
        _allNodes.Clear();
        _nodeByPath.Clear();
        _allAssets.Clear();
        _selectedDirectory = null;
        _selectedAsset = null;
        ActiveDirectoryPath.Value = "";
        _projectRootPath = "";
    }
}
