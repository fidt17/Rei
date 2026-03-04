using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IOPath = System.IO.Path;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Controls.Assets;

public sealed class AssetPickerViewModel : BaseViewModel
{
    public sealed class Entry
    {
        public string Name { get; }
        public string FullPath { get; }
        public string AssetId { get; }

        public Entry(string name, string fullPath, string assetId)
        {
            Name = name;
            FullPath = fullPath;
            AssetId = assetId;
        }
    }

    public const string EmptyAssetName = "empty";
    public const string MissingAssetName = "missing asset";
    public event Action? AssetSelectedEvent;
    public event Action? AssetActivatedEvent;

    public SearchFieldViewModel SearchField { get; } = new();
    public ObservableCollection<AssetSearchItemViewModel> SearchResults { get; } = new();

    public bool IsSelectionSupported => _useEntriesMode ? _entries.Count > 0 : _allowedExtensions.Count > 0;
    public bool HasActiveAsset => !string.IsNullOrWhiteSpace(SelectedAssetId) && !IsMissingAsset;

    #region AssetName

    private string _assetName = EmptyAssetName;
    public string AssetName
    {
        get => _assetName;
        private set => SetField(ref _assetName, value);
    }

    #endregion

    #region IsMissingAsset

    private bool _isMissingAsset;
    public bool IsMissingAsset
    {
        get => _isMissingAsset;
        private set => SetField(ref _isMissingAsset, value);
    }

    #endregion

    #region SelectedAssetId

    private string _selectedAssetId = "";
    public string SelectedAssetId
    {
        get => _selectedAssetId;
        private set => SetField(ref _selectedAssetId, value);
    }

    #endregion

    private readonly IAssetSearchService? _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IReadOnlyList<string> _allowedExtensions;
    private readonly bool _useEntriesMode;
    private readonly List<Entry> _entries = new();
    private readonly Dictionary<string, Entry> _entryById = new();
    private readonly Dictionary<string, Entry> _entryByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string?, string?>? _onSelectedAssetChanged;

    public AssetPickerViewModel(
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IReadOnlyList<string> allowedExtensions,
        Action<string?, string?>? onSelectedAssetChanged)
    {
        _assetSearchService = assetSearchService;
        _assetRegistry = assetRegistry;
        _allowedExtensions = allowedExtensions;
        _onSelectedAssetChanged = onSelectedAssetChanged;

        SearchField.Query.ChangedEvent += HandleSearchQueryChanged;
    }

    public AssetPickerViewModel(
        IAssetRegistry assetRegistry,
        IEnumerable<Entry> entries,
        Action<string?, string?>? onSelectedAssetChanged)
    {
        _assetRegistry = assetRegistry;
        _allowedExtensions = Array.Empty<string>();
        _onSelectedAssetChanged = onSelectedAssetChanged;
        _useEntriesMode = true;

        foreach (var entry in entries)
        {
            _entries.Add(entry);
            _entryById[entry.AssetId] = entry;
            _entryByPath[entry.FullPath] = entry;
        }

        SearchField.Query.ChangedEvent += HandleSearchQueryChanged;
    }

    public void SyncSelectedAsset(string? assetId)
    {
        SelectedAssetId = assetId?.Trim() ?? "";
        UpdateSelectedAssetState(SelectedAssetId);
    }

    public void ClearAsset()
    {
        CommitSelection("", null);
    }

    public void ActivateAsset()
    {
        if (!HasActiveAsset) return;

        AssetActivatedEvent?.Invoke();
    }

    public bool CanAcceptAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!IsSelectionSupported) return false;

        if (_useEntriesMode) return _entryByPath.ContainsKey(path);

        var extension = Path.GetExtension(path);
        if (!_allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;

        return _assetRegistry.TryGetByPath(path, out _);
    }

    public bool TryAssignAssetFromPath(string path)
    {
        if (_useEntriesMode)
        {
            if (!_entryByPath.TryGetValue(path, out var entry)) return false;
            CommitSelection(entry.AssetId, entry.FullPath);
            return true;
        }

        if (!CanAcceptAssetPath(path)) return false;
        if (!_assetRegistry.TryGetByPath(path, out var assetInfo) || assetInfo == null) return false;

        CommitSelection(assetInfo.Meta.AssetId, assetInfo.FullPath);
        return true;
    }

    public void RefreshSearchResultsForAll()
    {
        SearchResults.Clear();
        if (!IsSelectionSupported) return;

        if (_useEntriesMode)
        {
            foreach (var entry in _entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                SearchResults.Add(new AssetSearchItemViewModel(
                    entry.Name,
                    entry.FullPath,
                    entry.AssetId,
                    () => CommitSelection(entry.AssetId, entry.FullPath)));
            }

            return;
        }

        foreach (var asset in _assetRegistry.GetAllAssetsByExtensions(_allowedExtensions))
        {
            SearchResults.Add(new AssetSearchItemViewModel(
                IOPath.GetFileNameWithoutExtension(asset.FullPath),
                asset.FullPath,
                asset.Meta.AssetId,
                () => CommitSelection(asset.Meta.AssetId, asset.FullPath)));
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        SearchField.Query.ChangedEvent -= HandleSearchQueryChanged;
        SearchField.Dispose();
    }

    private void HandleSearchQueryChanged(string query)
    {
        if (SearchField.ShouldSuppressQueryRefresh()) return;
        RefreshSearchResults(query);
    }

    private void RefreshSearchResults(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            RefreshSearchResultsForAll();
            return;
        }

        SearchResults.Clear();
        if (!IsSelectionSupported) return;

        if (_useEntriesMode)
        {
            foreach (var entry in _entries.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                SearchResults.Add(new AssetSearchItemViewModel(
                    entry.Name,
                    entry.FullPath,
                    entry.AssetId,
                    () => CommitSelection(entry.AssetId, entry.FullPath)));
            }

            return;
        }

        if (_assetSearchService == null) return;

        var results = _assetSearchService.SearchByExtensions(query, _allowedExtensions);
        foreach (var result in results)
        {
            if (result.IsDirectory) continue;
            if (!_assetRegistry.TryGetByPath(result.FullPath, out var assetInfo) || assetInfo == null) continue;

            SearchResults.Add(new AssetSearchItemViewModel(
                IOPath.GetFileNameWithoutExtension(result.FullPath),
                result.FullPath,
                assetInfo.Meta.AssetId,
                () => CommitSelection(assetInfo.Meta.AssetId, result.FullPath)));
        }
    }

    private void CommitSelection(string? assetId, string? fullPath)
    {
        SelectedAssetId = assetId?.Trim() ?? "";
        UpdateSelectedAssetState(SelectedAssetId);
        _onSelectedAssetChanged?.Invoke(SelectedAssetId, fullPath);
        AssetSelectedEvent?.Invoke();
    }

    private void UpdateSelectedAssetState(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            AssetName = EmptyAssetName;
            IsMissingAsset = false;
            return;
        }

        if (_useEntriesMode)
        {
            if (!_entryById.TryGetValue(assetId, out var entry))
            {
                AssetName = MissingAssetName;
                IsMissingAsset = true;
                return;
            }

            AssetName = entry.Name;
            IsMissingAsset = false;
            return;
        }

        if (!_assetRegistry.TryGetById(assetId, out var assetInfo) || assetInfo == null)
        {
            AssetName = MissingAssetName;
            IsMissingAsset = true;
            return;
        }

        var extension = Path.GetExtension(assetInfo.FullPath);
        if (!IsSelectionSupported || !_allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            AssetName = MissingAssetName;
            IsMissingAsset = true;
            return;
        }

        AssetName = Path.GetFileNameWithoutExtension(assetInfo.FullPath);
        IsMissingAsset = false;
    }
}
