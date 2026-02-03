using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class AssetPropertyViewModel : BaseCustomPropertyViewModel
{
    public event Action? AssetSelectedEvent;

    public SearchFieldViewModel SearchField { get; } = new();
    public ObservableCollection<AssetSearchItemViewModel> SearchResults { get; } = new();
    public ObservableField<bool> HasResults { get; } = new(false);
    public ObservableField<bool> IsSelectionSupported { get; } = new(false);

    private string _assetName = "empty";
    public string AssetName
    {
        get => _assetName;
        private set => SetField(ref _assetName, value);
    }

    private bool _isMissingAsset;
    public bool IsMissingAsset
    {
        get => _isMissingAsset;
        private set => SetField(ref _isMissingAsset, value);
    }

    private readonly IAssetSearchService _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private IReadOnlyList<string> _allowedExtensions = Array.Empty<string>();
    private bool _isInitialized;

#pragma warning disable CS8618
    public AssetPropertyViewModel() { }
#pragma warning restore CS8618

    public AssetPropertyViewModel(
        SerializedProperty property,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper) : base(property)
    {
        if (property.Type != SerializedTypeEnum.Custom) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Custom}. Actual {property.Type}");

        _assetSearchService = assetSearchService;
        _assetRegistry = assetRegistry;

        var templateTypeName = property.TemplateTypeName;
        var assetType = assetTypeMapper.GetAssetTypeForTemplateType(templateTypeName);
        _allowedExtensions = assetTypeMapper.GetExtensionsForAssetType(assetType);
        IsSelectionSupported.Value = assetType != AssetType.Unknown;

        SearchField.Query.ChangedEvent += HandleSearchQueryChanged;
        var idProperty = GetNestedProperty("Id");
        if (idProperty != null)
        {
            idProperty.ValueChangedEvent += HandleIdValueChangedEvent;
        }

        _isInitialized = true;
        UpdateAssetDisplay(GetAssetId());
    }

    public override void Dispose()
    {
        base.Dispose();

        SearchField.Query.ChangedEvent -= HandleSearchQueryChanged;
        var idProperty = GetNestedProperty("Id");
        if (idProperty != null)
        {
            idProperty.ValueChangedEvent -= HandleIdValueChangedEvent;
        }
    }

    public void ClearAsset()
    {
        var idProperty = GetNestedProperty("Id");
        if (idProperty == null) return;

        idProperty.Value = "";
    }

    public bool CanAcceptAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (_allowedExtensions.Count == 0) return false;

        var extension = Path.GetExtension(path);
        if (!_allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;

        return _assetRegistry.TryGetByPath(path, out _);
    }

    public bool TryAssignAssetFromPath(string path)
    {
        if (!CanAcceptAssetPath(path)) return false;
        if (!_assetRegistry.TryGetByPath(path, out var assetInfo) || assetInfo == null) return false;

        SelectAsset(assetInfo.Meta.AssetId);
        return true;
    }

    public void RefreshSearchResultsForAll()
    {
        SearchResults.Clear();

        if (_allowedExtensions.Count == 0)
        {
            HasResults.Value = false;
            return;
        }

        foreach (var asset in _assetRegistry.GetAllAssetsByExtensions(_allowedExtensions))
        {
            SearchResults.Add(new AssetSearchItemViewModel(
                Path.GetFileNameWithoutExtension(asset.FullPath),
                asset.FullPath,
                asset.Meta.AssetId,
                () => SelectAsset(asset.Meta.AssetId)));
        }

        HasResults.Value = SearchResults.Count > 0;
    }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        if (!_isInitialized) return;
        UpdateAssetDisplay(GetAssetId());
    }

    private void HandleIdValueChangedEvent(object? value)
    {
        if (!_isInitialized) return;
        UpdateAssetDisplay(value as string);
    }

    private string? GetAssetId()
    {
        var idProperty = GetNestedProperty("Id");
        return idProperty?.Value as string;
    }

    private void UpdateAssetDisplay(string? assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            AssetName = "empty";
            IsMissingAsset = false;
            return;
        }

        if (!_assetRegistry.TryGetById(assetId, out var assetInfo))
        {
            AssetName = "missing asset";
            IsMissingAsset = true;
            return;
        }

        var extension = Path.GetExtension(assetInfo.FullPath);
        if (_allowedExtensions.Count > 0 && !_allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            AssetName = "missing asset";
            IsMissingAsset = true;
            return;
        }

        AssetName = Path.GetFileNameWithoutExtension(assetInfo.FullPath);
        IsMissingAsset = false;
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

        var results = _assetSearchService.SearchByExtensions(query, _allowedExtensions);
        foreach (var result in results)
        {
            if (result.IsDirectory) continue;

            if (!_assetRegistry.TryGetByPath(result.FullPath, out var assetInfo)) continue;

            SearchResults.Add(new AssetSearchItemViewModel(
                Path.GetFileNameWithoutExtension(result.FullPath),
                result.FullPath,
                assetInfo.Meta.AssetId,
                () => SelectAsset(assetInfo.Meta.AssetId)));
        }

        HasResults.Value = SearchResults.Count > 0;
    }

    private void SelectAsset(string assetId)
    {
        var idProperty = GetNestedProperty("Id");
        if (idProperty == null) return;

        idProperty.Value = assetId;
        AssetSelectedEvent?.Invoke();
    }

    
}
