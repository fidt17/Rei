using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using IOPath = System.IO.Path;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.AssetCreation.Behaviour;
using ReiEditor.Models.EditorApp.AssetCreation.Material;
using ReiEditor.Models.EditorApp.AssetCreation.Shader;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.EditorApp.SettingsWindow;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;
using ReiEditor.ViewModels.Windows.Editor.Project.Directories;
using ReiEditor.ViewModels.Windows.Editor.Project.Path;
using ReiEditor.ViewModels.Windows.Editor.Project.Services;

namespace ReiEditor.ViewModels.Windows.Editor.Project;

public class ProjectWindowViewModel : BaseViewModel
{
    public event Action<string>? ScrollToAssetRequested;

    public ObservableCollection<ProjectDirectoryNodeViewModel> RootDirectories => _directoryBrowser.RootDirectories;
    public ObservableCollection<ProjectAssetItemViewModel> ActiveItems { get; } = new();
    public ObservableCollection<ProjectPathSegmentViewModel> PathSegments => _directoryBrowser.PathSegments;
    public ObservableField<string> ActiveDirectoryPath => _directoryBrowser.ActiveDirectoryPath;
    public SearchFieldViewModel SearchField { get; } = new();

    public ContextMenuViewModel ActiveFolderContextMenu { get; } = new();

    private readonly IResourceService? _resourceService;
    private readonly IAssetRegistry? _assetRegistry;
    private readonly IBehaviourCreationWindowService? _behaviourCreationWindowService;
    private readonly IMaterialCreationWindowService? _materialCreationWindowService;
    private readonly IShaderCreationWindowService? _shaderCreationWindowService;
    private readonly IEditorRefreshService? _editorRefreshService;
    private readonly IProjectAssetFocusService? _projectAssetFocusService;
    private readonly IFileExplorerProvider? _fileExplorerProvider;
    private readonly ISceneAssetDragSessionService? _sceneAssetDragSessionService;

    private readonly ProjectDirectoryBrowser _directoryBrowser;
    private readonly ProjectAssetItemBuilder _assetItemBuilder;
    private readonly ProjectAssetSelectionHandler _assetSelectionHandler;
    private readonly ProjectAssetOperationsHandler _assetOperationsHandler;
    private readonly ProjectWindowActionsController _actionsController;

    private ProjectAssetItemViewModel? _highlightedAsset;
    private string _pendingSearchSelectionPath = "";

#pragma warning disable CS8618
    public ProjectWindowViewModel()
    {
        _directoryBrowser = new ProjectDirectoryBrowser(
            () => SearchField.HasQuery.Value,
            SearchField.ResetSearch,
            HandleDirectorySelected);
        _assetItemBuilder = new ProjectAssetItemBuilder(null, null, null);
        _assetSelectionHandler = new ProjectAssetSelectionHandler(null, TrackSearchSelection);
        _assetOperationsHandler = new ProjectAssetOperationsHandler(null, null, null, null, null, null);
        _actionsController = new ProjectWindowActionsController(
            _directoryBrowser,
            _assetSelectionHandler,
            _assetOperationsHandler,
            null,
            null,
            () => ActiveItems,
            ApplyCommandResult,
            ApplyBatchCommandResult);

        SetupContextMenus();
    }
#pragma warning restore CS8618

    public ProjectWindowViewModel(
        IResourceService resourceService,
        IStorageProvider storageProvider,
        IAssetRegistry assetRegistry,
        IAssetOperationsService assetOperationsService,
        IProjectAssetDeleteCommand projectAssetDeleteCommand,
        IProjectAssetDuplicateCommand projectAssetDuplicateCommand,
        IProjectAssetMoveCommand projectAssetMoveCommand,
        IProjectAssetRenameCommand projectAssetRenameCommand,
        IFileExplorerProvider fileExplorerProvider,
        IAssetSearchService assetSearchService,
        IEditorRefreshService editorRefreshService,
        ITextEditorFileOpener textEditorFileOpener,
        ISettingsWindowService settingsWindowService,
        IBehaviourCreationWindowService behaviourCreationWindowService,
        IMaterialCreationWindowService materialCreationWindowService,
        IShaderCreationWindowService shaderCreationWindowService,
        ISelectionService selectionService,
        IProjectAssetFocusService projectAssetFocusService,
        ISceneAssetDragSessionService sceneAssetDragSessionService)
    {
        _resourceService = resourceService;
        _assetRegistry = assetRegistry;
        _behaviourCreationWindowService = behaviourCreationWindowService;
        _materialCreationWindowService = materialCreationWindowService;
        _shaderCreationWindowService = shaderCreationWindowService;
        _editorRefreshService = editorRefreshService;
        _projectAssetFocusService = projectAssetFocusService;
        _fileExplorerProvider = fileExplorerProvider;
        _sceneAssetDragSessionService = sceneAssetDragSessionService;

        _directoryBrowser = new ProjectDirectoryBrowser(() => SearchField.HasQuery.Value, SearchField.ResetSearch, HandleDirectorySelected);
        _assetItemBuilder = new ProjectAssetItemBuilder(assetRegistry, assetSearchService, fileExplorerProvider);
        _assetSelectionHandler = new ProjectAssetSelectionHandler(selectionService, TrackSearchSelection);
        _assetOperationsHandler = new ProjectAssetOperationsHandler(storageProvider, assetOperationsService, projectAssetDeleteCommand, projectAssetDuplicateCommand, projectAssetMoveCommand, projectAssetRenameCommand);
        _actionsController = new ProjectWindowActionsController(_directoryBrowser, _assetSelectionHandler, _assetOperationsHandler, textEditorFileOpener, settingsWindowService, () => ActiveItems, ApplyCommandResult, ApplyBatchCommandResult);

        SetupContextMenus();
        _directoryBrowser.BuildTree(resourceService);
        SearchField.Query.ChangedEvent += HandleSearchQueryChanged;
        _editorRefreshService.RefreshedEvent += HandleEditorRefreshedEvent;
        _projectAssetFocusService.FocusAssetRequested += HandleFocusAssetRequestedEvent;
        _projectAssetFocusService.FocusAssetPathRequested += HandleFocusAssetPathRequestedEvent;
    }

    public override void Dispose()
    {
        base.Dispose();
        SearchField.Query.ChangedEvent -= HandleSearchQueryChanged;
        if (_editorRefreshService != null)
        {
            _editorRefreshService.RefreshedEvent -= HandleEditorRefreshedEvent;
        }

        if (_projectAssetFocusService != null)
        {
            _projectAssetFocusService.FocusAssetRequested -= HandleFocusAssetRequestedEvent;
            _projectAssetFocusService.FocusAssetPathRequested -= HandleFocusAssetPathRequestedEvent;
        }

        ResetProjectView();
    }

    private void HandleEditorRefreshedEvent()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            RefreshView(affectsTree: true);
        });
    }

    private void HandleFocusAssetRequestedEvent(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId)) return;
        if (_assetRegistry == null) return;
        if (!_assetRegistry.TryGetById(assetId, out var assetInfo) || assetInfo == null) return;

        Dispatcher.UIThread.InvokeAsync(() => FocusAssetByPath(assetInfo.FullPath));
    }

    private void HandleFocusAssetPathRequestedEvent(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return;

        Dispatcher.UIThread.InvokeAsync(() => FocusAssetByPath(assetPath));
    }

    private void SetupContextMenus()
    {
        var createMenu = new ContextMenuViewModel();
        createMenu.AddOption(new ContextMenuOption("Folder", CreateFolder));
        createMenu.AddOption(new ContextMenuOption("Behaviour", OpenCreateBehaviourOverlay));
        createMenu.AddOption(new ContextMenuOption("Shader", OpenCreateShaderOverlay));
        createMenu.AddOption(new ContextMenuOption("Material", OpenCreateMaterialOverlay));

        ActiveFolderContextMenu.AddOption(new ContextMenuOption("Show in Explorer", OpenActiveFolderInExplorer));
        ActiveFolderContextMenu.AddOption(new ContextMenuOption("Create", createMenu));
    }

    private void OpenActiveFolderInExplorer()
    {
        if (_fileExplorerProvider == null) return;

        var activePath = ActiveDirectoryPath.Value;
        if (string.IsNullOrWhiteSpace(activePath)) return;

        _fileExplorerProvider.OpenDirectory(activePath);
    }

    private void HandleDirectorySelected(string directoryPath)
    {
        UpdateActiveItems(directoryPath);
    }

    private void UpdateActiveItems(string directoryPath)
    {
        _highlightedAsset = null;
        ActiveItems.ClearAndDispose();

        if (!Directory.Exists(directoryPath)) return;

        var items = SearchField.HasQuery.Value
            ? _assetItemBuilder.BuildItemsForSearch(SearchField.Query.Value, ActiveFolderContextMenu, CreateAssetItemActions())
            : _assetItemBuilder.BuildItemsForDirectory(directoryPath, ActiveFolderContextMenu, CreateAssetItemActions());
        foreach (var item in items)
        {
            ActiveItems.Add(item);
        }

        _assetSelectionHandler.RestoreSelection(ActiveItems);
    }

    private ProjectAssetItemActions CreateAssetItemActions()
    {
        return _actionsController.CreateAssetItemActions(
            HandleAssetSelectionRequested,
            HandleAssetContextMenuSelectionRequested);
    }

    public async Task ImportExternalPathsAsync(IReadOnlyCollection<string> paths)
    {
        await _assetOperationsHandler.ImportExternalPathsAsync(paths, ActiveDirectoryPath.Value);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RefreshView(affectsTree: true);
        });
    }

    public IReadOnlyList<string> GetDraggedAssetPaths(ProjectAssetItemViewModel sourceItem)
    {
        return _assetSelectionHandler.ResolveDraggedAssetPaths(sourceItem, ActiveItems);
    }

    public bool CanStartSceneAssetDrag(IReadOnlyList<string> assetPaths)
    {
        return _sceneAssetDragSessionService != null && _sceneAssetDragSessionService.CanStart(assetPaths);
    }

    public void StartSceneAssetDrag(IReadOnlyList<string> assetPaths)
    {
        _sceneAssetDragSessionService?.Start(assetPaths);
    }

    public void HandleSceneAssetDesktopDragCompleted(DragDropEffects result)
    {
        _sceneAssetDragSessionService?.HandleDesktopDragCompleted(result);
    }

    public async Task MoveAssetsToDirectoryAsync(IReadOnlyCollection<string> assetPaths, string destinationDirectory)
    {
        var targets = _assetSelectionHandler.ResolveCommandTargets(assetPaths);
        if (targets.Count == 0) return;

        var result = await _assetOperationsHandler.MoveAsync(targets, destinationDirectory, _directoryBrowser.ProjectRootPath);
        if (result == null) return;

        ApplyBatchCommandResult(result);
    }

    public void ClearAssetSelection()
    {
        _assetSelectionHandler.ClearSelection(ActiveItems);
    }

    private void RefreshView(bool affectsTree)
    {
        var activePath = _directoryBrowser.SelectedDirectoryPath ?? ActiveDirectoryPath.Value;
        if (affectsTree && _resourceService != null)
        {
            _directoryBrowser.BuildTree(_resourceService, activePath);
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

        _directoryBrowser.OpenDirectory(targetDirectory);
        _assetSelectionHandler.SelectAssetByPath(targetPath, ActiveItems);
    }

    private void FocusAssetByPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return;

        var directoryPath = IOPath.GetDirectoryName(assetPath);
        if (string.IsNullOrWhiteSpace(directoryPath)) return;

        _directoryBrowser.OpenDirectory(directoryPath);
        HighlightAssetByPath(assetPath);
    }

    private void HighlightAssetByPath(string path)
    {
        _highlightedAsset?.ClearHighlight();
        _highlightedAsset = null;

        foreach (var item in ActiveItems)
        {
            if (!string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase)) continue;

            item.PulseHighlight(TimeSpan.FromSeconds(1));
            _highlightedAsset = item;
            ScrollToAssetRequested?.Invoke(path);
            return;
        }
    }

    private void HandleAssetSelectionRequested(ProjectAssetItemViewModel item, Avalonia.Input.KeyModifiers modifiers)
    {
        _assetSelectionHandler.HandleSelectionRequested(item, modifiers, ActiveItems);
    }

    private void HandleAssetContextMenuSelectionRequested(ProjectAssetItemViewModel item)
    {
        _assetSelectionHandler.HandleContextMenuSelectionRequested(item, ActiveItems);
    }

    private void ApplyCommandResult(ProjectAssetCommandResult result)
    {
        _assetSelectionHandler.SetSelectionState(
            string.IsNullOrWhiteSpace(result.SelectedAssetPath) ? Array.Empty<string>() : new[] { result.SelectedAssetPath },
            result.SelectedAssetPath,
            result.SelectedAssetPath);
        RefreshView(result.AffectsTree);
    }

    private void ApplyBatchCommandResult(ProjectAssetBatchCommandResult result)
    {
        _assetSelectionHandler.SetSelectionState(result.SelectedAssetPaths, result.PrimarySelectedAssetPath, result.SelectionAnchorAssetPath);
        RefreshView(result.AffectsTree);
    }

    private void ResetProjectView()
    {
        ActiveItems.ClearAndDispose();
        _directoryBrowser.Reset();
        _assetSelectionHandler.ResetState();
        _highlightedAsset = null;
        _pendingSearchSelectionPath = "";
    }

    private void CreateFolder()
    {
        _ = _assetOperationsHandler.CreateFolderAsync(ActiveDirectoryPath.Value).ContinueWith(_ =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshView(affectsTree: true);
            });
        });
    }

    private void OpenCreateBehaviourOverlay()
    {
        if (_behaviourCreationWindowService == null) return;

        var targetDirectory = ActiveDirectoryPath.Value;
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory)) return;

        _behaviourCreationWindowService.OpenBehaviourCreationWindow(targetDirectory, () =>
        {
            Dispatcher.UIThread.InvokeAsync(() => RefreshView(affectsTree: false));
        });
    }

    private void OpenCreateShaderOverlay()
    {
        if (_shaderCreationWindowService == null) return;

        var targetDirectory = ActiveDirectoryPath.Value;
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory)) return;

        _shaderCreationWindowService.OpenShaderCreationWindow(targetDirectory, () =>
        {
            Dispatcher.UIThread.InvokeAsync(() => RefreshView(affectsTree: false));
        });
    }

    private void OpenCreateMaterialOverlay()
    {
        if (_materialCreationWindowService == null) return;

        var targetDirectory = ActiveDirectoryPath.Value;
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory)) return;

        _materialCreationWindowService.OpenMaterialCreationWindow(targetDirectory, () =>
        {
            Dispatcher.UIThread.InvokeAsync(() => RefreshView(affectsTree: false));
        });
    }
}
