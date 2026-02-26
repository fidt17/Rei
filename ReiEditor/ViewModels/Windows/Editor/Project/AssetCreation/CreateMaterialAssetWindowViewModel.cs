using System;
using ReiEditor.Models.EditorApp.AssetCreation.Material;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Creation.Material;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls.Assets;

namespace ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;

public class CreateMaterialAssetWindowViewModel : BaseViewModel
{
    public RelayCommand CreateCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AssetPickerViewModel ShaderPicker { get; }

    private readonly string _targetDirectory;
    private readonly Action _onCreated;
    private readonly IMaterialCreationWindowService _windowService;
    private readonly IMaterialCreationUtility _materialCreationUtility;

    #region MaterialName

    private string _materialName = "NewMaterial";
    public string MaterialName
    {
        get => _materialName;
        set => SetField(ref _materialName, value);
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

        CreateCommand = new RelayCommand(CreateAsset);
        CancelCommand = new RelayCommand(() => _windowService.CloseMaterialCreationWindow());

        ShaderPicker = new AssetPickerViewModel(
            assetSearchService,
            assetRegistry,
            new[] { FileExtensions.RSHADER },
            SelectShaderAsset);
        ShaderPicker.RefreshSearchResultsForAll();
    }

    public override void Dispose()
    {
        base.Dispose();
        ShaderPicker.Dispose();
    }

    private async void CreateAsset()
    {
        if (string.IsNullOrWhiteSpace(ShaderPicker.SelectedAssetId))
        {
            ErrorText = "Please select a shader asset.";
            return;
        }

        var settings = new MaterialCreationSettings(
            _targetDirectory,
            MaterialName,
            ShaderPicker.SelectedAssetId);

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

    private void SelectShaderAsset(string? _, string? __)
    {
        ErrorText = "";
    }
}
