using System;
using ReiEditor.Models.EditorApp.AssetCreation.Behaviour;
using ReiEditor.Models.Services.Assets.Creation.Behaviour;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;

public class CreateBehaviourAssetWindowViewModel : BaseViewModel
{
    public RelayCommand CreateCommand { get; }
    public RelayCommand CancelCommand { get; }

    private readonly string _targetDirectory;
    private readonly Action _onCreated;
    private readonly IBehaviourCreationUtility _behaviourCreationUtility;
    private readonly IBehaviourCreationWindowService _windowService;

    #region BehaviourName

    private string _behaviourName = "NewBehaviour";
    public string BehaviourName
    {
        get => _behaviourName;
        set => SetField(ref _behaviourName, value);
    }

    #endregion

    #region OverrideInit

    private bool _overrideInit;
    public bool OverrideInit
    {
        get => _overrideInit;
        set => SetField(ref _overrideInit, value);
    }

    #endregion

    #region OverrideStart

    private bool _overrideStart;
    public bool OverrideStart
    {
        get => _overrideStart;
        set => SetField(ref _overrideStart, value);
    }

    #endregion

    #region OverrideUpdate

    private bool _overrideUpdate = true;
    public bool OverrideUpdate
    {
        get => _overrideUpdate;
        set => SetField(ref _overrideUpdate, value);
    }

    #endregion

    #region OverrideDispose

    private bool _overrideDispose;
    public bool OverrideDispose
    {
        get => _overrideDispose;
        set => SetField(ref _overrideDispose, value);
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
    public CreateBehaviourAssetWindowViewModel() { }
#pragma warning restore CS8618

    public CreateBehaviourAssetWindowViewModel(
        string targetDirectory,
        Action onCreated,
        IBehaviourCreationWindowService windowService,
        IBehaviourCreationUtility behaviourCreationUtility)
    {
        _targetDirectory = targetDirectory;
        _onCreated = onCreated;
        _windowService = windowService;
        _behaviourCreationUtility = behaviourCreationUtility;

        CreateCommand = new RelayCommand(CreateAsset);
        CancelCommand = new RelayCommand(() => _windowService.CloseBehaviourCreationWindow());
    }

    private async void CreateAsset()
    {
        var settings = new BehaviourCreationSettings(
            _targetDirectory,
            BehaviourName,
            OverrideInit,
            OverrideStart,
            OverrideUpdate,
            OverrideDispose);

        var didCreate = await _behaviourCreationUtility.CreateBehaviourAsync(settings);

        if (!didCreate)
        {
            ErrorText = "Failed to create behaviour. Name must be unique and file should not already exist.";
            return;
        }

        ErrorText = "";
        _onCreated.Invoke();
        _windowService.CloseBehaviourCreationWindow();
    }
}
