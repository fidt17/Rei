using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.ViewModels.Common;

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
            _engineApi.ChangeTransformationMode(worldSpace: IsWorldSpace);
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
        _engineApi.ChangeTransformationMode(worldSpace);
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
