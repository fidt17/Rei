using System;
using System.Collections.ObjectModel;
using IOPath = System.IO.Path;
using ReiEditor.Models.EditorApp.AssetCreation.Material;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Creation.Material;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;

public class CreateMaterialAssetWindowViewModel : BaseViewModel
{
    public RelayCommand CreateCommand { get; }
    public RelayCommand CancelCommand { get; }
    public SearchFieldViewModel SearchField { get; } = new();
    public ObservableCollection<ShaderAssetSearchItemViewModel> ShaderSearchResults { get; } = new();

    private readonly string _targetDirectory;
    private readonly Action _onCreated;
    private readonly IMaterialCreationWindowService _windowService;
    private readonly IMaterialCreationUtility _materialCreationUtility;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetSearchService _assetSearchService;
    private string _selectedShaderAssetId = "";

    #region MaterialName

    private string _materialName = "NewMaterial";
    public string MaterialName
    {
        get => _materialName;
        set => SetField(ref _materialName, value);
    }

    #endregion

    #region SelectedShaderName

    private string _selectedShaderName = "None";
    public string SelectedShaderName
    {
        get => _selectedShaderName;
        private set => SetField(ref _selectedShaderName, value);
    }

    #endregion

    #region ErrorText

    private string _errorText = "";
    public string ErrorText
    {
        get => _errorText;
        private set => SetField(ref _errorText, value);
    }

    #endregion

#pragma warning disable CS8618
    public CreateMaterialAssetWindowViewModel() { }
#pragma warning restore CS8618

    public CreateMaterialAssetWindowViewModel(
        string targetDirectory,
        Action onCreated,
        IMaterialCreationWindowService windowService,
        IMaterialCreationUtility materialCreationUtility,
        IAssetRegistry assetRegistry,
        IAssetSearchService assetSearchService)
    {
        _targetDirectory = targetDirectory;
        _onCreated = onCreated;
        _windowService = windowService;
        _materialCreationUtility = materialCreationUtility;
        _assetRegistry = assetRegistry;
        _assetSearchService = assetSearchService;

        CreateCommand = new RelayCommand(CreateAsset);
        CancelCommand = new RelayCommand(() => _windowService.CloseMaterialCreationWindow());

        SearchField.Query.ChangedEvent += HandleSearchQueryChanged;
        RefreshShaderResultsForAll();
    }

    public override void Dispose()
    {
        base.Dispose();
        SearchField.Query.ChangedEvent -= HandleSearchQueryChanged;
        SearchField.Dispose();
    }

    private async void CreateAsset()
    {
        if (string.IsNullOrWhiteSpace(_selectedShaderAssetId))
        {
            ErrorText = "Please select a shader asset.";
            return;
        }

        var settings = new MaterialCreationSettings(
            _targetDirectory,
            MaterialName,
            _selectedShaderAssetId);

        var didCreate = await _materialCreationUtility.CreateMaterialAsync(settings);
        if (!didCreate)
        {
            ErrorText = "Failed to create material. Name must be valid/unique and shader should exist.";
            return;
        }

        ErrorText = "";
        _onCreated.Invoke();
        _windowService.CloseMaterialCreationWindow();
    }

    private void HandleSearchQueryChanged(string query)
    {
        if (SearchField.ShouldSuppressQueryRefresh()) return;

        if (string.IsNullOrWhiteSpace(query))
        {
            RefreshShaderResultsForAll();
            return;
        }

        RefreshShaderResults(query);
    }

    private void RefreshShaderResultsForAll()
    {
        ShaderSearchResults.Clear();
        foreach (var asset in _assetRegistry.GetAllAssetsByExtensions(new[] { FileExtensions.RSHADER }))
        {
            ShaderSearchResults.Add(new ShaderAssetSearchItemViewModel(
                IOPath.GetFileNameWithoutExtension(asset.FullPath),
                asset.FullPath,
                asset.Meta.AssetId,
                () => SelectShaderAsset(asset.Meta.AssetId, asset.FullPath)));
        }
    }

    private void RefreshShaderResults(string query)
    {
        ShaderSearchResults.Clear();
        var results = _assetSearchService.SearchByExtensions(query, new[] { FileExtensions.RSHADER });
        foreach (var result in results)
        {
            if (result.IsDirectory) continue;
            if (!_assetRegistry.TryGetByPath(result.FullPath, out var assetInfo) || assetInfo == null) continue;

            ShaderSearchResults.Add(new ShaderAssetSearchItemViewModel(
                IOPath.GetFileNameWithoutExtension(result.FullPath),
                result.FullPath,
                assetInfo.Meta.AssetId,
                () => SelectShaderAsset(assetInfo.Meta.AssetId, result.FullPath)));
        }
    }

    private void SelectShaderAsset(string assetId, string fullPath)
    {
        _selectedShaderAssetId = assetId;
        SelectedShaderName = IOPath.GetFileNameWithoutExtension(fullPath);
        ErrorText = "";
    }
}
