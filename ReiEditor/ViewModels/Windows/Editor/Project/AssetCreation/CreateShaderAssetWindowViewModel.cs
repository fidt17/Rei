using System;
using ReiEditor.Models.EditorApp.AssetCreation.Shader;
using ReiEditor.Models.Services.Assets.Creation.Shader;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;

public class CreateShaderAssetWindowViewModel : BaseViewModel
{
    public RelayCommand CreateCommand { get; }
    public RelayCommand CancelCommand { get; }

    private readonly string _targetDirectory;
    private readonly Action _onCreated;
    private readonly IShaderCreationWindowService _windowService;
    private readonly IShaderCreationUtility _shaderCreationUtility;

    #region ShaderName

    private string _shaderName = "NewShader";
    public string ShaderName
    {
        get => _shaderName;
        set => SetField(ref _shaderName, value);
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
    public CreateShaderAssetWindowViewModel() { }
#pragma warning restore CS8618

    public CreateShaderAssetWindowViewModel(
        string targetDirectory,
        Action onCreated,
        IShaderCreationWindowService windowService,
        IShaderCreationUtility shaderCreationUtility)
    {
        _targetDirectory = targetDirectory;
        _onCreated = onCreated;
        _windowService = windowService;
        _shaderCreationUtility = shaderCreationUtility;

        CreateCommand = new RelayCommand(CreateAsset);
        CancelCommand = new RelayCommand(() => _windowService.CloseShaderCreationWindow());
    }

    private async void CreateAsset()
    {
        var settings = new ShaderCreationSettings(_targetDirectory, ShaderName);
        var didCreate = await _shaderCreationUtility.CreateShaderAsync(settings);
        if (!didCreate)
        {
            ErrorText = "Failed to create shader. Name must be valid and unique.";
            return;
        }

        ErrorText = "";
        _onCreated.Invoke();
        _windowService.CloseShaderCreationWindow();
    }
}
