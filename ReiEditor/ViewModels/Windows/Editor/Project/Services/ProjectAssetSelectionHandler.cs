using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using IOPath = System.IO.Path;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Services;

public class ProjectAssetSelectionHandler
{
    private readonly HashSet<string> _selectedAssetPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISelectionService? _selectionService;
    private readonly Action<ProjectAssetItemViewModel> _selectionTrackedAction;

    private string? _primarySelectedAssetPath;
    private string? _selectionAnchorAssetPath;

    public ProjectAssetSelectionHandler(ISelectionService? selectionService, Action<ProjectAssetItemViewModel> selectionTrackedAction)
    {
        _selectionService = selectionService;
        _selectionTrackedAction = selectionTrackedAction;
    }

    public void HandleSelectionRequested(ProjectAssetItemViewModel item, KeyModifiers modifiers, IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            SelectAssetRange(item, modifiers.HasFlag(KeyModifiers.Control), activeItems);
            return;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            ToggleAssetSelection(item, activeItems);
            return;
        }

        ReplaceAssetSelection(item, activeItems);
    }

    public void HandleContextMenuSelectionRequested(ProjectAssetItemViewModel item, IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        if (IsAssetSelected(item))
        {
            SetPrimarySelectedAsset(item, activeItems);
            return;
        }

        ReplaceAssetSelection(item, activeItems);
    }

    public void RestoreSelection(IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        var visiblePaths = activeItems
            .Select(item => item.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _selectedAssetPaths.RemoveWhere(path => !visiblePaths.Contains(path));

        if (_selectedAssetPaths.Count == 0)
        {
            _primarySelectedAssetPath = null;
            _selectionAnchorAssetPath = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_primarySelectedAssetPath) || !_selectedAssetPaths.Contains(_primarySelectedAssetPath))
            {
                _primarySelectedAssetPath = GetFirstVisibleSelectedAssetPath(activeItems);
            }

            if (string.IsNullOrWhiteSpace(_selectionAnchorAssetPath) || !visiblePaths.Contains(_selectionAnchorAssetPath))
            {
                _selectionAnchorAssetPath = _primarySelectedAssetPath;
            }
        }

        UpdateVisibleAssetSelectionState(activeItems);
    }

    public void SelectAssetByPath(string path, IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        var match = activeItems.FirstOrDefault(item => string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;

        ReplaceAssetSelection(match, activeItems);
    }

    public void ClearSelection(IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        ClearSelectionState();
        UpdateVisibleAssetSelectionState(activeItems);
    }

    public void ResetState()
    {
        ClearSelectionState();
    }

    public void SetSelectionState(IReadOnlyCollection<string> selectedPaths, string? primarySelectedPath, string? selectionAnchorPath)
    {
        _selectedAssetPaths.Clear();
        foreach (var selectedPath in selectedPaths)
        {
            _selectedAssetPaths.Add(selectedPath);
        }

        _primarySelectedAssetPath = primarySelectedPath;
        _selectionAnchorAssetPath = selectionAnchorPath;
    }

    public IReadOnlyList<ProjectAssetCommandTarget> ResolveCommandTargets(ProjectAssetItemViewModel sourceItem, IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        IReadOnlyCollection<string> selectedPaths = IsAssetSelected(sourceItem)
            ? _selectedAssetPaths
            : new HashSet<string>(new[] { sourceItem.FullPath }, StringComparer.OrdinalIgnoreCase);
        return ResolveCommandTargets(selectedPaths);
    }

    public IReadOnlyList<ProjectAssetCommandTarget> ResolveCommandTargets(IReadOnlyCollection<string> assetPaths)
    {
        var orderedTargets = assetPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new ProjectAssetCommandTarget(path, System.IO.Directory.Exists(path)))
            .OrderBy(target => target.FullPath.Length)
            .ToList();

        var resolvedTargets = new List<ProjectAssetCommandTarget>();
        foreach (var target in orderedTargets)
        {
            if (resolvedTargets.Any(existing => existing.IsDirectory && IsSameOrDescendantPath(target.FullPath, existing.FullPath)))
            {
                continue;
            }

            resolvedTargets.Add(target);
        }

        return resolvedTargets;
    }

    public IReadOnlyList<string> ResolveDraggedAssetPaths(ProjectAssetItemViewModel sourceItem, IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        return ResolveCommandTargets(sourceItem, activeItems)
            .Select(target => target.FullPath)
            .ToList();
    }

    private void ReplaceAssetSelection(ProjectAssetItemViewModel item, IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        _selectedAssetPaths.Clear();
        _selectedAssetPaths.Add(item.FullPath);
        _selectionAnchorAssetPath = item.FullPath;
        _primarySelectedAssetPath = item.FullPath;
        _selectionTrackedAction(item);
        UpdateVisibleAssetSelectionState(activeItems);
    }

    private void ToggleAssetSelection(ProjectAssetItemViewModel item, IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        if (_selectedAssetPaths.Contains(item.FullPath))
        {
            _selectedAssetPaths.Remove(item.FullPath);

            if (string.Equals(_primarySelectedAssetPath, item.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                _primarySelectedAssetPath = GetFirstVisibleSelectedAssetPath(activeItems);
            }
        }
        else
        {
            _selectedAssetPaths.Add(item.FullPath);
            _primarySelectedAssetPath = item.FullPath;
            _selectionTrackedAction(item);
        }

        _selectionAnchorAssetPath = item.FullPath;
        UpdateVisibleAssetSelectionState(activeItems);
    }

    private void SelectAssetRange(ProjectAssetItemViewModel item, bool addToExistingSelection, IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        var targetIndex = GetItemIndex(activeItems, item.FullPath);
        if (targetIndex < 0)
        {
            ReplaceAssetSelection(item, activeItems);
            return;
        }

        var anchorIndex = GetAnchorIndex(activeItems, targetIndex);
        if (!addToExistingSelection)
        {
            _selectedAssetPaths.Clear();
        }

        var start = Math.Min(anchorIndex, targetIndex);
        var end = Math.Max(anchorIndex, targetIndex);
        for (var i = start; i <= end; i++)
        {
            _selectedAssetPaths.Add(activeItems[i].FullPath);
        }

        _primarySelectedAssetPath = item.FullPath;
        _selectionTrackedAction(item);
        UpdateVisibleAssetSelectionState(activeItems);
    }

    private static int GetItemIndex(IReadOnlyList<ProjectAssetItemViewModel> activeItems, string assetPath)
    {
        for (var i = 0; i < activeItems.Count; i++)
        {
            if (string.Equals(activeItems[i].FullPath, assetPath, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private int GetAnchorIndex(IReadOnlyList<ProjectAssetItemViewModel> activeItems, int targetIndex)
    {
        if (string.IsNullOrWhiteSpace(_selectionAnchorAssetPath))
        {
            _selectionAnchorAssetPath = activeItems[targetIndex].FullPath;
            return targetIndex;
        }

        var anchorIndex = -1;
        for (var i = 0; i < activeItems.Count; i++)
        {
            if (!string.Equals(activeItems[i].FullPath, _selectionAnchorAssetPath, StringComparison.OrdinalIgnoreCase)) continue;

            anchorIndex = i;
            break;
        }

        if (anchorIndex >= 0) return anchorIndex;

        _selectionAnchorAssetPath = activeItems[targetIndex].FullPath;
        return targetIndex;
    }

    private void UpdateVisibleAssetSelectionState(IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        foreach (var asset in activeItems)
        {
            asset.SetSelected(_selectedAssetPaths.Contains(asset.FullPath));
        }

        SyncPrimaryAssetSelection(activeItems);
    }

    private void SyncPrimaryAssetSelection(IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        if (_selectionService == null) return;

        var selectedItems = activeItems
            .Where(asset => _selectedAssetPaths.Contains(asset.FullPath))
            .Cast<ISelectable>()
            .ToList();

        var primaryItem = GetPrimarySelectedAssetItem(activeItems);
        if (selectedItems.Count == 0 || primaryItem == null || primaryItem.IsDirectory)
        {
            if (_selectionService.SelectedItems.OfType<IAssetSelectable>().Any())
            {
                _selectionService.ResetSelection(sendToEngine: false);
            }

            return;
        }

        _selectionService.SetSelection(selectedItems, primaryItem);
    }

    private void SetPrimarySelectedAsset(ProjectAssetItemViewModel item, IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        _primarySelectedAssetPath = item.FullPath;
        _selectionTrackedAction(item);
        UpdateVisibleAssetSelectionState(activeItems);
    }

    private string? GetFirstVisibleSelectedAssetPath(IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        return activeItems.FirstOrDefault(item => _selectedAssetPaths.Contains(item.FullPath))?.FullPath;
    }

    private ProjectAssetItemViewModel? GetPrimarySelectedAssetItem(IReadOnlyList<ProjectAssetItemViewModel> activeItems)
    {
        if (string.IsNullOrWhiteSpace(_primarySelectedAssetPath)) return null;

        return activeItems.FirstOrDefault(item =>
            string.Equals(item.FullPath, _primarySelectedAssetPath, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsAssetSelected(ProjectAssetItemViewModel item)
    {
        return _selectedAssetPaths.Contains(item.FullPath);
    }

    private void ClearSelectionState()
    {
        _selectedAssetPaths.Clear();
        _primarySelectedAssetPath = null;
        _selectionAnchorAssetPath = null;
    }

    private static bool IsSameOrDescendantPath(string path, string rootPath)
    {
        var normalizedPath = NormalizeDirectoryPath(path);
        var normalizedRootPath = NormalizeDirectoryPath(rootPath);
        return normalizedPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        var fullPath = IOPath.GetFullPath(path)
            .TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
        return fullPath + IOPath.DirectorySeparatorChar;
    }
}
