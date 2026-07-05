using Avalonia.Threading;
using ReiEditor.Models.Services.TransformationControls;
using ReiEditor.ViewModels.Common;
using ReactiveUI;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class TransformationControlSettingsViewModel : BaseViewModel
{
    public bool CanUseLocalSpace => _transformationControlsService.CanUseLocalSpace;
    public bool CanUseWorldSpace => _transformationControlsService.CanUseWorldSpace;
    public bool IsLocalSpace => _transformationControlsService.IsLocalSpace;
    public bool IsWorldSpace => _transformationControlsService.IsWorldSpace;
    public bool CanUseRectTransformMode => _transformationControlsService.CanUseRectTransformMode;
    public bool EngineRunning => _transformationControlsService.EngineRunning;

    public bool MovementMode
    {
        get => _transformationControlsService.Mode == TransformationMode.Movement;
        set => ApplyTransformationModeFromToggle(TransformationMode.Movement, value, nameof(MovementMode));
    }

    public bool ScaleMode
    {
        get => _transformationControlsService.Mode == TransformationMode.Scale;
        set => ApplyTransformationModeFromToggle(TransformationMode.Scale, value, nameof(ScaleMode));
    }

    public bool RotationMode
    {
        get => _transformationControlsService.Mode == TransformationMode.Rotation;
        set => ApplyTransformationModeFromToggle(TransformationMode.Rotation, value, nameof(RotationMode));
    }

    public bool RectTransformMode
    {
        get => _transformationControlsService.Mode == TransformationMode.RectTransform;
        set => ApplyTransformationModeFromToggle(TransformationMode.RectTransform, value, nameof(RectTransformMode));
    }

    private readonly ITransformationControlsService _transformationControlsService;

#pragma warning disable CS8618
    public TransformationControlSettingsViewModel() { }
#pragma warning restore CS8618

    public TransformationControlSettingsViewModel(ITransformationControlsService transformationControlsService)
    {
        _transformationControlsService = transformationControlsService;
        _transformationControlsService.StateChanged += HandleStateChangedEvent;
    }

    public override void Dispose()
    {
        base.Dispose();
        _transformationControlsService.StateChanged -= HandleStateChangedEvent;
    }

    public void SetWorldSpace()
    {
        _transformationControlsService.SetWorldSpace();
    }

    public void SetLocalSpace()
    {
        _transformationControlsService.SetLocalSpace();
    }

    public void SetMovementMode()
    {
        _transformationControlsService.SetMode(TransformationMode.Movement);
    }

    public void SetScaleMode()
    {
        _transformationControlsService.SetMode(TransformationMode.Scale);
    }

    public void SetRotationMode()
    {
        _transformationControlsService.SetMode(TransformationMode.Rotation);
    }

    public void SetRectTransformMode()
    {
        _transformationControlsService.SetMode(TransformationMode.RectTransform);
    }

    private void ApplyTransformationModeFromToggle(TransformationMode mode, bool isChecked, string propertyName)
    {
        if (isChecked)
        {
            _transformationControlsService.SetMode(mode);
            return;
        }

        this.RaisePropertyChanged(propertyName);
    }

    private void HandleStateChangedEvent()
    {
        Dispatcher.UIThread.Post(RaiseAllPropertyChanged);
    }

    private void RaiseAllPropertyChanged()
    {
        this.RaisePropertyChanged(nameof(CanUseLocalSpace));
        this.RaisePropertyChanged(nameof(CanUseWorldSpace));
        this.RaisePropertyChanged(nameof(IsLocalSpace));
        this.RaisePropertyChanged(nameof(IsWorldSpace));
        this.RaisePropertyChanged(nameof(CanUseRectTransformMode));
        this.RaisePropertyChanged(nameof(EngineRunning));
        this.RaisePropertyChanged(nameof(MovementMode));
        this.RaisePropertyChanged(nameof(ScaleMode));
        this.RaisePropertyChanged(nameof(RotationMode));
        this.RaisePropertyChanged(nameof(RectTransformMode));
    }
}
