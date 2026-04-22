using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.TransformationControls;
using ReiEditor.ViewModels.Common;
using ReactiveUI;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class TransformationControlSettingsViewModel : BaseViewModel
{
    #region CanUseLocalSpace

    private bool _canUseLocalSpace = true;
    public bool CanUseLocalSpace
    {
        get => _canUseLocalSpace;
        private set => SetField(ref _canUseLocalSpace, value);
    }

    #endregion

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
        set => ApplyTransformationModeFromToggle(TransformationMode.Movement, value, nameof(MovementMode));
    }

    #endregion

    #region ScaleMode

    private bool _scaleMode;
    public bool ScaleMode
    {
        get => _scaleMode;
        set => ApplyTransformationModeFromToggle(TransformationMode.Scale, value, nameof(ScaleMode));
    }

    #endregion

    #region RotationMode

    private bool _rotationMode;
    public bool RotationMode
    {
        get => _rotationMode;
        set => ApplyTransformationModeFromToggle(TransformationMode.Rotation, value, nameof(RotationMode));
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
    private readonly ISelectionService _selectionService;

#pragma warning disable CS8618
    public TransformationControlSettingsViewModel() { }
#pragma warning restore CS8618

    public TransformationControlSettingsViewModel(IEngineApi engineApi, IEngineRunner engineRunner, ISelectionService selectionService)
    {
        _engineApi = engineApi;
        _engineRunner = engineRunner;
        _selectionService = selectionService;

        _engineRunner.IsActive.Subscribe(HandleEngineIsActiveValueChangedEvent);
        _selectionService.SelectionChanged.Subscribe(HandleSelectionChangedEvent);
        UpdateSelectionModeState(_selectionService.SelectedItems);
    }

    private void HandleEngineIsActiveValueChangedEvent(bool isActive)
    {
        EngineRunning = isActive;

        if (isActive)
        {
            ApplyTransformationMode(GetSelectedMode(), IsWorldSpace);
        }
    }

    private void HandleSelectionChangedEvent(IReadOnlyCollection<ISelectable> selectedItems)
    {
        UpdateSelectionModeState(selectedItems);
    }

    private void UpdateSelectionModeState(IReadOnlyCollection<ISelectable> selectedItems)
    {
        var selectedEntityCount = selectedItems
            .OfType<IEntitySelectable>()
            .Select(selectable => selectable.Entity.Id)
            .Distinct()
            .Count();

        CanUseLocalSpace = selectedEntityCount <= 1;
        if (CanUseLocalSpace || !IsLocalSpace) return;

        ApplySpaceMode(worldSpace: true);
    }

    private void ApplySpaceMode(bool worldSpace)
    {
        IsWorldSpace = worldSpace;
        IsLocalSpace = !worldSpace;
        ApplyTransformationMode(GetSelectedMode(), worldSpace);
    }

    public override void Dispose()
    {
        base.Dispose();

        _engineRunner.IsActive.Unsubscribe(HandleEngineIsActiveValueChangedEvent);
        _selectionService.SelectionChanged.Unsubscribe(HandleSelectionChangedEvent);
    }

    public void SetWorldSpace()
    {
        ApplySpaceMode(worldSpace: true);
    }

    public void SetLocalSpace()
    {
        if (!CanUseLocalSpace) return;

        ApplySpaceMode(worldSpace: false);
    }

    public void SetMovementMode()
    {
        ApplyTransformationMode(TransformationMode.Movement, IsWorldSpace);
    }

    public void SetScaleMode()
    {
        ApplyTransformationMode(TransformationMode.Scale, IsWorldSpace);
    }

    public void SetRotationMode()
    {
        ApplyTransformationMode(TransformationMode.Rotation, IsWorldSpace);
    }

    private TransformationMode GetSelectedMode()
    {
        if (ScaleMode) return TransformationMode.Scale;
        if (RotationMode) return TransformationMode.Rotation;
        return TransformationMode.Movement;
    }

    private void ApplyTransformationMode(TransformationMode mode, bool worldSpace)
    {
        SetField(ref _movementMode, mode == TransformationMode.Movement, nameof(MovementMode));
        SetField(ref _scaleMode, mode == TransformationMode.Scale, nameof(ScaleMode));
        SetField(ref _rotationMode, mode == TransformationMode.Rotation, nameof(RotationMode));
        _engineApi.ChangeTransformationMode(mode, worldSpace);
    }

    private void ApplyTransformationModeFromToggle(TransformationMode mode, bool isChecked, string propertyName)
    {
        if (isChecked)
        {
            ApplyTransformationMode(mode, IsWorldSpace);
            return;
        }

        this.RaisePropertyChanged(propertyName);
    }
}
