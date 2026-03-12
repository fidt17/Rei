using System.Collections.Generic;
using System.IO;
using System.Linq;
using IOPath = System.IO.Path;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Services;

public class ProjectAssetItemBuilder
{
    private readonly IAssetRegistry? _assetRegistry;
    private readonly IAssetSearchService? _assetSearchService;
    private readonly IFileExplorerProvider? _fileExplorerProvider;

    public ProjectAssetItemBuilder(
        IAssetRegistry? assetRegistry,
        IAssetSearchService? assetSearchService,
        IFileExplorerProvider? fileExplorerProvider)
    {
        _assetRegistry = assetRegistry;
        _assetSearchService = assetSearchService;
        _fileExplorerProvider = fileExplorerProvider;
    }

    public IReadOnlyList<ProjectAssetItemViewModel> BuildItemsForDirectory(string directoryPath, ContextMenuViewModel activeFolderContextMenu, ProjectAssetItemActions actions)
    {
        var items = new List<ProjectAssetItemViewModel>();
        if (!Directory.Exists(directoryPath) || _fileExplorerProvider == null) return items;

        foreach (var directory in Directory.EnumerateDirectories(directoryPath).OrderBy(IOPath.GetFileName))
        {
            if (AssetFileFilter.ShouldHideDirectory(directory)) continue;

            items.Add(CreateItem(IOPath.GetFileName(directory), directory, ProjectAssetType.Directory, "", actions, activeFolderContextMenu));
        }

        foreach (var file in Directory.EnumerateFiles(directoryPath).OrderBy(IOPath.GetFileName))
        {
            if (AssetFileFilter.ShouldHide(file)) continue;

            items.Add(CreateItem(IOPath.GetFileName(file), file, GetAssetType(file), ResolveAssetId(file), actions, activeFolderContextMenu));
        }

        return items;
    }

    public IReadOnlyList<ProjectAssetItemViewModel> BuildItemsForSearch(string query, ContextMenuViewModel activeFolderContextMenu, ProjectAssetItemActions actions)
    {
        var items = new List<ProjectAssetItemViewModel>();
        if (_assetSearchService == null || _fileExplorerProvider == null) return items;

        var results = _assetSearchService.Search(query);
        foreach (var result in results)
        {
            var assetType = result.IsDirectory ? ProjectAssetType.Directory : GetAssetType(result.FullPath);
            var assetId = result.IsDirectory ? "" : ResolveAssetId(result.FullPath);
            items.Add(CreateItem(result.Name, result.FullPath, assetType, assetId, actions, activeFolderContextMenu));
        }

        return items;
    }

    private ProjectAssetItemViewModel CreateItem(string name, string fullPath, ProjectAssetType assetType, string assetId, ProjectAssetItemActions actions, ContextMenuViewModel activeFolderContextMenu)
    {
        return new ProjectAssetItemViewModel(name, fullPath, assetType, assetId, actions, activeFolderContextMenu, _fileExplorerProvider!);
    }

    private string ResolveAssetId(string filePath)
    {
        return _assetRegistry != null &&
               _assetRegistry.TryGetByPath(filePath, out var assetInfo) &&
               assetInfo != null
            ? assetInfo.Meta.AssetId : "";
    }

    private static ProjectAssetType GetAssetType(string filePath)
    {
        var extension = IOPath.GetExtension(filePath);
        if (extension == FileExtensions.SCENE) return ProjectAssetType.Scene;
        if (extension is FileExtensions.H or FileExtensions.CPP) return ProjectAssetType.Script;

        return ProjectAssetType.Asset;
    }
}
