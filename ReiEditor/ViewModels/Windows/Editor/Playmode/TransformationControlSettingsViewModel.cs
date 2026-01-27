using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class TransformationControlSettingsViewModel : BaseViewModel
{
    #region IsLocalSpace

    private bool _isLocalSpace;
    public bool IsLocalSpace
    {
        get => _isLocalSpace;
        private set => SetField(ref _isLocalSpace, value);
    }

    #endregion
    
    #region IsWorldSpace

    private bool _isWorldSpace = true;
    public bool IsWorldSpace
    {
        get => _isWorldSpace;
        private set => SetField(ref _isWorldSpace, value);
    }

    #endregion

    #region MovementMode

    private bool _movementMode = true;
    public bool MovementMode
    {
        get => _movementMode;
        private set
        {
            if (SetField(ref _movementMode, value))
            {
                SetMovementMode();
            }
        }
    }

    #endregion
    
    #region ScaleMode

    private bool _scaleMode;
    public bool ScaleMode
    {
        get => _scaleMode;
        private set
        {
            if (SetField(ref _scaleMode, value))
            {
                SetScaleMode();
            }
        }
    }

    #endregion
    
    #region RotationMode

    private bool _rotationMode;
    public bool RotationMode
    {
        get => _rotationMode;
        private set
        {
            if (SetField(ref _rotationMode, value))
            {
                SetRotationMode();
            }
        }
    }

    #endregion
    
    #region EngineRunning

    private bool _engineRunning = true;
    public bool EngineRunning
    {
        get => _engineRunning;
        private set => SetField(ref _engineRunning, value);
    }

    #endregion
    
    private readonly IEngineApi _engineApi;
    private readonly IEngineRunner _engineRunner;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public TransformationControlSettingsViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public TransformationControlSettingsViewModel(IEngineApi engineApi, IEngineRunner engineRunner)
    {
        _engineApi = engineApi;
        _engineRunner = engineRunner;
        
        _engineRunner.IsActive.Subscribe(HandleEngineIsActiveValueChangedEvent);
    }

    private void HandleEngineIsActiveValueChangedEvent(bool isActive)
    {
        EngineRunning = isActive;
        
        if (isActive)
        {
            _engineApi.ChangeTransformationMode(worldSpace: IsWorldSpace);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _engineRunner.IsActive.Unsubscribe(HandleEngineIsActiveValueChangedEvent);
    }

    public void SetWorldSpace()
    {
        IsWorldSpace = true;
        IsLocalSpace = false;
        _engineApi.ChangeTransformationMode(worldSpace: true);
    }

    public void SetLocalSpace()
    {
        IsWorldSpace = false;
        IsLocalSpace = true;
        _engineApi.ChangeTransformationMode(worldSpace: false);
    }
    
    public void SetMovementMode()
    {
        MovementMode = true;
        ScaleMode = false;
        RotationMode = false;
    }

    public void SetScaleMode()
    {
        MovementMode = false;
        ScaleMode = true;
        RotationMode = false;
    }

    public void SetRotationMode()
    {
        MovementMode = false;
        ScaleMode = false;
        RotationMode = true;
    }
}