using System;
using System.Collections.Generic;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.EditorApp.SettingsWindow;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Services;

public class ProjectWindowActionsController
{
    private readonly ProjectDirectoryBrowser _directoryBrowser;
    private readonly ProjectAssetSelectionHandler _assetSelectionHandler;
    private readonly ProjectAssetOperationsHandler _assetOperationsHandler;
    private readonly ITextEditorFileOpener? _textEditorFileOpener;
    private readonly ISettingsWindowService? _settingsWindowService;
    private readonly Func<IReadOnlyList<ProjectAssetItemViewModel>> _activeItemsProvider;
    private readonly Action<ProjectAssetCommandResult> _commandResultHandler;
    private readonly Action<ProjectAssetBatchCommandResult> _batchCommandResultHandler;

    public ProjectWindowActionsController(
        ProjectDirectoryBrowser directoryBrowser,
        ProjectAssetSelectionHandler assetSelectionHandler,
        ProjectAssetOperationsHandler assetOperationsHandler,
        ITextEditorFileOpener? textEditorFileOpener,
        ISettingsWindowService? settingsWindowService,
        Func<IReadOnlyList<ProjectAssetItemViewModel>> activeItemsProvider,
        Action<ProjectAssetCommandResult> commandResultHandler,
        Action<ProjectAssetBatchCommandResult> batchCommandResultHandler)
    {
        _directoryBrowser = directoryBrowser;
        _assetSelectionHandler = assetSelectionHandler;
        _assetOperationsHandler = assetOperationsHandler;
        _textEditorFileOpener = textEditorFileOpener;
        _settingsWindowService = settingsWindowService;
        _activeItemsProvider = activeItemsProvider;
        _commandResultHandler = commandResultHandler;
        _batchCommandResultHandler = batchCommandResultHandler;
    }

    public ProjectAssetItemActions CreateAssetItemActions(Action<ProjectAssetItemViewModel, Avalonia.Input.KeyModifiers> selectionRequestedAction, Action<ProjectAssetItemViewModel> contextMenuSelectionRequestedAction)
    {
        return new ProjectAssetItemActions(selectionRequestedAction, contextMenuSelectionRequestedAction, DeleteAsset, DuplicateAsset, RenameAsset, MoveAsset, OpenAsset);
    }

    private void OpenAsset(ProjectAssetItemViewModel item)
    {
        if (item.IsDirectory)
        {
            _directoryBrowser.OpenDirectory(item.FullPath);
            return;
        }

        if (_textEditorFileOpener == null) return;

        var result = _textEditorFileOpener.Open(item.FullPath);
        if (result != TextEditorOpenResult.InvalidCustomEditorPath) return;
        if (_settingsWindowService == null) return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var prompt = MessageBoxManager.GetMessageBoxStandard(
                "Text Editor",
                "Configured text editor path is invalid. Open Editor Settings?",
                ButtonEnum.YesNo);
            var dialogResult = await prompt.ShowAsync();

            if (dialogResult == ButtonResult.Yes)
            {
                _settingsWindowService.OpenSettingsWindow();
            }
        });
    }

    private void RenameAsset(ProjectAssetItemViewModel item, string newName)
    {
        var task = _assetOperationsHandler.RenameAsync(item, newName);
        if (task == null) return;

        _ = task.ContinueWith(t =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _commandResultHandler(t.Result);
            });
        });
    }

    private void DeleteAsset(ProjectAssetItemViewModel item)
    {
        var activeItems = _activeItemsProvider();
        var targets = _assetSelectionHandler.ResolveCommandTargets(item, activeItems);
        _ = _assetOperationsHandler.DeleteAsync(targets).ContinueWith(t =>
        {
            var result = t.Result;
            if (result == null) return;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _batchCommandResultHandler(result);
            });
        });
    }

    private void MoveAsset(ProjectAssetItemViewModel item)
    {
        var projectRootPath = _directoryBrowser.ProjectRootPath;
        var activeItems = _activeItemsProvider();
        var targets = _assetSelectionHandler.ResolveCommandTargets(item, activeItems);
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var result = await _assetOperationsHandler.MoveAsync(targets, projectRootPath);
            if (result == null) return;

            _batchCommandResultHandler(result);
        });
    }

    private void DuplicateAsset(ProjectAssetItemViewModel item)
    {
        var activeItems = _activeItemsProvider();
        var targets = _assetSelectionHandler.ResolveCommandTargets(item, activeItems);
        _ = _assetOperationsHandler.DuplicateAsync(targets).ContinueWith(t =>
        {
            var result = t.Result;
            if (result == null) return;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _batchCommandResultHandler(result);
            });
        });
    }
}
