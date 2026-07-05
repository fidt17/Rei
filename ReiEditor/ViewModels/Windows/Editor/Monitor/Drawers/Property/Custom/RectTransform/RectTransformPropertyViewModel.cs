using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.RectTransform;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.RectTransform;

public class RectTransformPropertyViewModel : BaseViewModel
{
    public static readonly string[] OwnedPropertyNames =
    {
        EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN,
        EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX,
        EngineBehavioursConstants.RECT_TRANSFORM_PIVOT,
        EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION,
        EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA
    };

    public ObservableCollection<RectTransformAnchorPresetViewModel> Presets { get; } = new();
    public RelayCommand ToggleRawAnchorsCommand { get; }

    private readonly GameEntity? _entity;
    private readonly BehaviourComponent? _component;
    private readonly IEngineRunner? _engineRunner;
    private readonly IEntityApi? _entityApi;
    private readonly IRectTransformLayoutService? _rectTransformLayoutService;
    private readonly Vector2PropertyBinding _anchorMin;
    private readonly Vector2PropertyBinding _anchorMax;
    private readonly Vector2PropertyBinding _pivot;
    private readonly Vector2PropertyBinding _anchoredPosition;
    private readonly Vector2PropertyBinding _sizeDelta;

    private string _anchorModeName = "Preset";
    public string AnchorModeName
    {
        get => _anchorModeName;
        set => SetField(ref _anchorModeName, value);
    }

    private string _anchorPresetName = "Middle Center";
    public string AnchorPresetName
    {
        get => _anchorPresetName;
        set => SetField(ref _anchorPresetName, value);
    }

    private bool _isRawAnchorsVisible;
    public bool IsRawAnchorsVisible
    {
        get => _isRawAnchorsVisible;
        set => SetField(ref _isRawAnchorsVisible, value);
    }

    private bool _isStretchX;
    public bool IsStretchX
    {
        get => _isStretchX;
        set => SetField(ref _isStretchX, value);
    }

    private bool _isStretchY;
    public bool IsStretchY
    {
        get => _isStretchY;
        set => SetField(ref _isStretchY, value);
    }

    public string HorizontalPrimaryLabel => IsStretchX ? "Left" : "Pos X";
    public string HorizontalSecondaryLabel => IsStretchX ? "Right" : "Width";
    public string VerticalPrimaryLabel => IsStretchY ? "Top" : "Pos Y";
    public string VerticalSecondaryLabel => IsStretchY ? "Bottom" : "Height";
    public string StretchXLabel => IsStretchX ? "Stretch X" : "Fixed X";
    public string StretchYLabel => IsStretchY ? "Stretch Y" : "Fixed Y";

    public float HorizontalPrimaryValue
    {
        get => IsStretchX ? GetLeft() : PositionX;
        set
        {
            if (IsStretchX)
            {
                SetLeft(value);
            }
            else
            {
                PositionX = value;
            }
        }
    }

    public float HorizontalSecondaryValue
    {
        get => IsStretchX ? GetRight() : SizeDeltaX;
        set
        {
            if (IsStretchX)
            {
                SetRight(value);
            }
            else
            {
                SizeDeltaX = value;
            }
        }
    }

    public float VerticalPrimaryValue
    {
        get => IsStretchY ? GetTop() : PositionY;
        set
        {
            if (IsStretchY)
            {
                SetTop(value);
            }
            else
            {
                PositionY = value;
            }
        }
    }

    public float VerticalSecondaryValue
    {
        get => IsStretchY ? GetBottom() : SizeDeltaY;
        set
        {
            if (IsStretchY)
            {
                SetBottom(value);
            }
            else
            {
                SizeDeltaY = value;
            }
        }
    }

    public float AnchorMinX
    {
        get => _anchorMin.X;
        set => SetAnchorMin(value, AnchorMinY);
    }

    public float AnchorMinY
    {
        get => _anchorMin.Y;
        set => SetAnchorMin(AnchorMinX, value);
    }

    public float AnchorMaxX
    {
        get => _anchorMax.X;
        set => SetAnchorMax(value, AnchorMaxY);
    }

    public float AnchorMaxY
    {
        get => _anchorMax.Y;
        set => SetAnchorMax(AnchorMaxX, value);
    }

    public float PivotX
    {
        get => _pivot.X;
        set => SetPivot(value, PivotY);
    }

    public float PivotY
    {
        get => _pivot.Y;
        set => SetPivot(PivotX, value);
    }

    private float PositionX
    {
        get => _anchoredPosition.X;
        set => SetAnchoredPosition(value, PositionY);
    }

    private float PositionY
    {
        get => _anchoredPosition.Y;
        set => SetAnchoredPosition(PositionX, value);
    }

    private float SizeDeltaX
    {
        get => _sizeDelta.X;
        set => SetSizeDelta(value, SizeDeltaY);
    }

    private float SizeDeltaY
    {
        get => _sizeDelta.Y;
        set => SetSizeDelta(SizeDeltaX, value);
    }

#pragma warning disable CS8618
    public RectTransformPropertyViewModel() { }
#pragma warning restore CS8618

    public RectTransformPropertyViewModel(
        GameEntity entity,
        BehaviourComponent component,
        IEngineRunner engineRunner,
        IEntityApi entityApi,
        IRectTransformLayoutService rectTransformLayoutService)
    {
        _entity = entity;
        _component = component;
        _engineRunner = engineRunner;
        _entityApi = entityApi;
        _rectTransformLayoutService = rectTransformLayoutService;
        _anchorMin = new Vector2PropertyBinding(component.GetProperty(EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN));
        _anchorMax = new Vector2PropertyBinding(component.GetProperty(EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX));
        _pivot = new Vector2PropertyBinding(component.GetProperty(EngineBehavioursConstants.RECT_TRANSFORM_PIVOT));
        _anchoredPosition = new Vector2PropertyBinding(component.GetProperty(EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION));
        _sizeDelta = new Vector2PropertyBinding(component.GetProperty(EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA));
        ToggleRawAnchorsCommand = new RelayCommand(() => IsRawAnchorsVisible = !IsRawAnchorsVisible);

        foreach (var preset in RectTransformAnchorPresetUtils.Presets)
        {
            Presets.Add(new RectTransformAnchorPresetViewModel(preset, ApplyPreset));
        }

        _anchorMin.Changed += HandleAnchorChanged;
        _anchorMax.Changed += HandleAnchorChanged;
        _pivot.Changed += HandleLayoutValueChanged;
        _anchoredPosition.Changed += HandleLayoutValueChanged;
        _sizeDelta.Changed += HandleLayoutValueChanged;

        RefreshAll();
    }

    public static bool OwnsProperty(string propertyName) => OwnedPropertyNames.Contains(propertyName);

    public override void Dispose()
    {
        base.Dispose();

        _anchorMin.Changed -= HandleAnchorChanged;
        _anchorMax.Changed -= HandleAnchorChanged;
        _pivot.Changed -= HandleLayoutValueChanged;
        _anchoredPosition.Changed -= HandleLayoutValueChanged;
        _sizeDelta.Changed -= HandleLayoutValueChanged;
        _anchorMin.Dispose();
        _anchorMax.Dispose();
        _pivot.Dispose();
        _anchoredPosition.Dispose();
        _sizeDelta.Dispose();
    }

    private void ApplyPreset(RectTransformAnchorPreset preset)
    {
        var parentSize = GetParentSize();
        var before = CalculateRect(parentSize);
        var values = CalculateValuesForRect(parentSize, before, preset.MinX, preset.MinY, preset.MaxX, preset.MaxY);
        SetRectTransformSilently(preset.MinX, preset.MinY, preset.MaxX, preset.MaxY, values);
        PushRectTransformToRuntimeIfNeeded();
        RefreshAll();
    }

    private float GetLeft()
    {
        var parentSize = GetParentSize();
        return CalculateRect(parentSize).MinX - parentSize.X * AnchorMinX;
    }

    private float GetRight()
    {
        var parentSize = GetParentSize();
        return parentSize.X * AnchorMaxX - CalculateRect(parentSize).MaxX;
    }

    private float GetTop()
    {
        var parentSize = GetParentSize();
        return parentSize.Y * AnchorMaxY - CalculateRect(parentSize).MaxY;
    }

    private float GetBottom()
    {
        var parentSize = GetParentSize();
        return CalculateRect(parentSize).MinY - parentSize.Y * AnchorMinY;
    }

    private void SetLeft(float value)
    {
        var parentSize = GetParentSize();
        var rect = CalculateRect(parentSize);
        rect = rect with { MinX = parentSize.X * AnchorMinX + value };
        ApplyRect(parentSize, rect);
    }

    private void SetRight(float value)
    {
        var parentSize = GetParentSize();
        var rect = CalculateRect(parentSize);
        rect = rect with { MaxX = parentSize.X * AnchorMaxX - value };
        ApplyRect(parentSize, rect);
    }

    private void SetTop(float value)
    {
        var parentSize = GetParentSize();
        var rect = CalculateRect(parentSize);
        rect = rect with { MaxY = parentSize.Y * AnchorMaxY - value };
        ApplyRect(parentSize, rect);
    }

    private void SetBottom(float value)
    {
        var parentSize = GetParentSize();
        var rect = CalculateRect(parentSize);
        rect = rect with { MinY = parentSize.Y * AnchorMinY + value };
        ApplyRect(parentSize, rect);
    }

    private RectTransformRect CalculateRect(RectTransformVector2 parentSize)
    {
        return RectTransformLayoutCalculator.CalculateRect(
            parentSize,
            AnchorMinX,
            AnchorMinY,
            AnchorMaxX,
            AnchorMaxY,
            PivotX,
            PivotY,
            PositionX,
            PositionY,
            SizeDeltaX,
            SizeDeltaY);
    }

    private void ApplyRect(RectTransformVector2 parentSize, RectTransformRect rect)
    {
        var values = CalculateValuesForRect(parentSize, rect, AnchorMinX, AnchorMinY, AnchorMaxX, AnchorMaxY);

        ApplyLayoutValues(values);
    }

    private RectTransformLayoutValues CalculateValuesForRect(RectTransformVector2 parentSize, RectTransformRect rect, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY)
    {
        return RectTransformLayoutCalculator.CalculateValuesForRect(parentSize, rect, anchorMinX, anchorMinY, anchorMaxX, anchorMaxY, PivotX, PivotY);
    }

    private void SetRectTransformSilently(float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY, RectTransformLayoutValues values)
    {
        _anchorMin.SetSilently(anchorMinX, anchorMinY);
        _anchorMax.SetSilently(anchorMaxX, anchorMaxY);
        _anchoredPosition.SetSilently(values.AnchoredPositionX, values.AnchoredPositionY);
        _sizeDelta.SetSilently(values.SizeDeltaX, values.SizeDeltaY);
    }

    private void SetAnchorMin(float x, float y)
    {
        _anchorMin.SetSilently(x, y);
        PushRectTransformToRuntimeIfNeeded();
        RefreshAll();
    }

    private void SetAnchorMax(float x, float y)
    {
        _anchorMax.SetSilently(x, y);
        PushRectTransformToRuntimeIfNeeded();
        RefreshAll();
    }

    private void SetAnchoredPosition(float x, float y)
    {
        _anchoredPosition.SetSilently(x, y);
        PushRectTransformToRuntimeIfNeeded();
        RefreshAll();
    }

    private void SetSizeDelta(float x, float y)
    {
        _sizeDelta.SetSilently(x, y);
        PushRectTransformToRuntimeIfNeeded();
        RefreshAll();
    }

    private void ApplyLayoutValues(RectTransformLayoutValues values)
    {
        _sizeDelta.SetSilently(values.SizeDeltaX, values.SizeDeltaY);
        _anchoredPosition.SetSilently(values.AnchoredPositionX, values.AnchoredPositionY);
        PushRectTransformToRuntimeIfNeeded();
        RefreshAll();
    }

    private void PushRectTransformToRuntimeIfNeeded()
    {
        if (_entity == null || _component == null || _engineRunner?.IsActive.Value != true || _entityApi == null || _rectTransformLayoutService == null) return;

        _entityApi.SetData(new SetEntityDataRequest
        {
            SceneId = _entity.Id,
            Behaviours = new List<Dictionary<string, object?>>
            {
                new()
                {
                    { SetEntityDataRequest.REI_BEHAVIOUR_ID, _component.Id },
                    { EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN, _rectTransformLayoutService.SerializeVector2(new RectTransformVector2(AnchorMinX, AnchorMinY)) },
                    { EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX, _rectTransformLayoutService.SerializeVector2(new RectTransformVector2(AnchorMaxX, AnchorMaxY)) },
                    { EngineBehavioursConstants.RECT_TRANSFORM_PIVOT, _rectTransformLayoutService.SerializeVector2(new RectTransformVector2(PivotX, PivotY)) },
                    { EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, _rectTransformLayoutService.SerializeVector2(new RectTransformVector2(PositionX, PositionY)) },
                    { EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, _rectTransformLayoutService.SerializeVector2(new RectTransformVector2(SizeDeltaX, SizeDeltaY)) }
                }
            }
        });
    }

    private void SetPivot(float pivotX, float pivotY)
    {
        if (_entity != null
            && _component != null
            && _rectTransformLayoutService?.TryPreserveRectForPivot(_entity, _component, pivotX, pivotY, out var preservedLayout) == true)
        {
            _pivot.SetSilently(preservedLayout.Pivot.X, preservedLayout.Pivot.Y);
            _anchoredPosition.SetSilently(preservedLayout.AnchoredPosition.X, preservedLayout.AnchoredPosition.Y);
            _sizeDelta.SetSilently(preservedLayout.SizeDelta.X, preservedLayout.SizeDelta.Y);
            PushRectTransformToRuntimeIfNeeded();
            RefreshAll();
            return;
        }

        _pivot.SetSilently(pivotX, pivotY);
        PushRectTransformToRuntimeIfNeeded();
        RefreshAll();
    }

    private RectTransformVector2 GetParentSize()
    {
        return _entity == null || _rectTransformLayoutService == null
            ? new RectTransformVector2(1920f, 1080f)
            : _rectTransformLayoutService.GetParentSize(_entity);
    }

    private void HandleAnchorChanged()
    {
        RefreshAnchorState();
        RaiseAnchorPropertyChanges();
        RaiseCurrentValuePropertyChanges();
    }

    private void HandleLayoutValueChanged()
    {
        RaiseLayoutPropertyChanges();
        RaiseCurrentValuePropertyChanges();
    }

    private void RefreshAll()
    {
        RefreshAnchorState();
        RaiseAnchorPropertyChanges();
        RaiseLayoutPropertyChanges();
        RaiseCurrentValuePropertyChanges();
    }

    private void RefreshAnchorState()
    {
        var preset = RectTransformAnchorPresetUtils.FindMatchingPreset(AnchorMinX, AnchorMinY, AnchorMaxX, AnchorMaxY);
        AnchorModeName = preset == null ? "Custom" : "Preset";
        AnchorPresetName = preset?.DisplayName ?? "Custom";
        IsStretchX = RectTransformAnchorPresetUtils.IsStretch(AnchorMinX, AnchorMaxX);
        IsStretchY = RectTransformAnchorPresetUtils.IsStretch(AnchorMinY, AnchorMaxY);

        foreach (var presetViewModel in Presets)
        {
            presetViewModel.IsSelected = RectTransformAnchorPresetUtils.IsMatching(
                presetViewModel.Preset,
                AnchorMinX,
                AnchorMinY,
                AnchorMaxX,
                AnchorMaxY);
        }

        this.RaisePropertyChanged(nameof(HorizontalPrimaryLabel));
        this.RaisePropertyChanged(nameof(HorizontalSecondaryLabel));
        this.RaisePropertyChanged(nameof(VerticalPrimaryLabel));
        this.RaisePropertyChanged(nameof(VerticalSecondaryLabel));
        this.RaisePropertyChanged(nameof(StretchXLabel));
        this.RaisePropertyChanged(nameof(StretchYLabel));
    }

    private void RaiseAnchorPropertyChanges()
    {
        this.RaisePropertyChanged(nameof(AnchorMinX));
        this.RaisePropertyChanged(nameof(AnchorMinY));
        this.RaisePropertyChanged(nameof(AnchorMaxX));
        this.RaisePropertyChanged(nameof(AnchorMaxY));
    }

    private void RaiseLayoutPropertyChanges()
    {
        this.RaisePropertyChanged(nameof(PivotX));
        this.RaisePropertyChanged(nameof(PivotY));
    }

    private void RaiseCurrentValuePropertyChanges()
    {
        this.RaisePropertyChanged(nameof(HorizontalPrimaryValue));
        this.RaisePropertyChanged(nameof(HorizontalSecondaryValue));
        this.RaisePropertyChanged(nameof(VerticalPrimaryValue));
        this.RaisePropertyChanged(nameof(VerticalSecondaryValue));
    }
}
