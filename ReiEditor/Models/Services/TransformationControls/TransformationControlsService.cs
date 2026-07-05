using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.TransformationControls;

public class TransformationControlsService : ITransformationControlsService, IDisposable
{
    private const int RUNTIME_SYNC_INTERVAL_MS = 100;

    public event Action? StateChanged;

    public bool CanUseLocalSpace { get; private set; } = true;
    public bool CanUseWorldSpace { get; private set; } = true;
    public bool IsLocalSpace { get; private set; }
    public bool IsWorldSpace { get; private set; } = true;
    public bool CanUseRectTransformMode { get; private set; }
    public bool EngineRunning { get; private set; } = true;
    public TransformationMode Mode { get; private set; } = TransformationMode.Movement;

    private readonly IEngineApi _engineApi;
    private readonly IEngineRunner _engineRunner;
    private readonly ISelectionService _selectionService;
    private readonly IBehaviourRegistry _behaviourRegistry;

    private CancellationTokenSource? _runtimeSyncCts;
    private bool _hasRectTransformSelection;

    public TransformationControlsService(IEngineApi engineApi, IEngineRunner engineRunner, ISelectionService selectionService, IBehaviourRegistry behaviourRegistry)
    {
        _engineApi = engineApi;
        _engineRunner = engineRunner;
        _selectionService = selectionService;
        _behaviourRegistry = behaviourRegistry;

        _selectionService.SelectionChanged.Subscribe(HandleSelectionChangedEvent);
        UpdateSelectionModeState(_selectionService.SelectedItems);
        _engineRunner.IsActive.Subscribe(HandleEngineIsActiveValueChangedEvent);
    }

    public void Dispose()
    {
        StopRuntimeSync();
        _engineRunner.IsActive.Unsubscribe(HandleEngineIsActiveValueChangedEvent);
        _selectionService.SelectionChanged.Unsubscribe(HandleSelectionChangedEvent);
    }

    public void SetWorldSpace()
    {
        if (!CanUseWorldSpace) return;

        ApplySpaceMode(worldSpace: true);
    }

    public void SetLocalSpace()
    {
        if (!CanUseLocalSpace) return;

        ApplySpaceMode(worldSpace: false);
    }

    public void SetMode(TransformationMode mode)
    {
        ApplyTransformationMode(mode, IsWorldSpace);
    }

    private void HandleEngineIsActiveValueChangedEvent(bool isActive)
    {
        var engineRunningChanged = EngineRunning != isActive;
        EngineRunning = isActive;

        if (isActive)
        {
            ApplyTransformationMode(GetSelectedMode(), IsWorldSpace);
            StartRuntimeSync();
            if (engineRunningChanged) NotifyStateChanged();
        }
        else
        {
            StopRuntimeSync();
            NotifyStateChanged();
        }
    }

    private void HandleSelectionChangedEvent(IReadOnlyCollection<ISelectable> selectedItems)
    {
        UpdateSelectionModeState(selectedItems);
    }

    private void UpdateSelectionModeState(IReadOnlyCollection<ISelectable> selectedItems)
    {
        var oldCanUseLocalSpace = CanUseLocalSpace;
        var oldCanUseWorldSpace = CanUseWorldSpace;
        var oldCanUseRectTransformMode = CanUseRectTransformMode;

        var selectedEntities = selectedItems
            .OfType<IEntitySelectable>()
            .Select(selectable => selectable.Entity)
            .GroupBy(entity => entity.Id)
            .Select(group => group.First())
            .ToList();

        var rectTransformId = _behaviourRegistry.GetIdByName(EngineBehavioursConstants.RECT_TRANSFORM);
        _hasRectTransformSelection = rectTransformId != null && selectedEntities.Any(entity => entity.GetBehaviour(rectTransformId) != null);
        CanUseRectTransformMode = _hasRectTransformSelection;
        CanUseWorldSpace = !_hasRectTransformSelection;
        CanUseLocalSpace = selectedEntities.Count <= 1 || _hasRectTransformSelection;
        var capabilitiesChanged = oldCanUseLocalSpace != CanUseLocalSpace
                                  || oldCanUseWorldSpace != CanUseWorldSpace
                                  || oldCanUseRectTransformMode != CanUseRectTransformMode;

        if (!_hasRectTransformSelection && Mode == TransformationMode.RectTransform)
        {
            ApplyTransformationMode(TransformationMode.Movement, IsWorldSpace);
            if (capabilitiesChanged) NotifyStateChanged();
            return;
        }

        if (_hasRectTransformSelection)
        {
            ApplySpaceMode(worldSpace: false);
            if (capabilitiesChanged) NotifyStateChanged();
            return;
        }

        if (!CanUseLocalSpace && IsLocalSpace)
        {
            ApplySpaceMode(worldSpace: true);
            if (capabilitiesChanged) NotifyStateChanged();
            return;
        }

        if (capabilitiesChanged) NotifyStateChanged();
    }

    private void ApplySpaceMode(bool worldSpace)
    {
        ApplyTransformationMode(GetSelectedMode(), worldSpace);
    }

    private TransformationMode GetSelectedMode()
    {
        if (Mode == TransformationMode.RectTransform && CanUseRectTransformMode) return TransformationMode.RectTransform;
        return Mode;
    }

    private void ApplyTransformationMode(TransformationMode mode, bool worldSpace)
    {
        if (mode == TransformationMode.RectTransform && !CanUseRectTransformMode) return;

        SetTransformationModeState(mode, worldSpace);
        _engineApi.ChangeTransformationMode(mode, mode == TransformationMode.RectTransform ? false : worldSpace);
    }

    private bool SetTransformationModeState(TransformationMode mode, bool worldSpace)
    {
        if (mode == TransformationMode.RectTransform && !CanUseRectTransformMode) mode = TransformationMode.Movement;

        var isWorldSpace = mode != TransformationMode.RectTransform && worldSpace;
        var isLocalSpace = !isWorldSpace;

        var changed = Mode != mode || IsWorldSpace != isWorldSpace || IsLocalSpace != isLocalSpace;
        Mode = mode;
        IsWorldSpace = isWorldSpace;
        IsLocalSpace = isLocalSpace;

        if (changed) NotifyStateChanged();
        return changed;
    }

    private void StartRuntimeSync()
    {
        StopRuntimeSync();
        _runtimeSyncCts = new CancellationTokenSource();
        _ = RunRuntimeSync(_runtimeSyncCts.Token);
    }

    private void StopRuntimeSync()
    {
        _runtimeSyncCts?.Cancel();
        _runtimeSyncCts?.Dispose();
        _runtimeSyncCts = null;
    }

    private async Task RunRuntimeSync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(RUNTIME_SYNC_INTERVAL_MS));
            while (await timer.WaitForNextTickAsync(token))
            {
                SyncRuntimeTransformationMode();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when engine stops or service is disposed.
        }
    }

    private void SyncRuntimeTransformationMode()
    {
        if (!_engineRunner.IsActive.Value) return;

        int modeValue;
        try
        {
            modeValue = _engineApi.GetTransformationMode();
        }
        catch
        {
            return;
        }

        if (modeValue < 0) return;

        var mode = (TransformationMode) modeValue;
        if (!Enum.IsDefined(typeof(TransformationMode), mode)) return;

        SetTransformationModeState(mode, IsWorldSpace);
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
