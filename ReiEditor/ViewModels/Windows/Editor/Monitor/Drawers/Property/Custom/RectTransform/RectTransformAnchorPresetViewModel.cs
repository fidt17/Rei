using System;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.RectTransform;

public class RectTransformAnchorPresetViewModel : BaseViewModel
{
    public string DisplayName => Preset.DisplayName;
    public string ButtonText => Preset.ButtonText;
    public RelayCommand ApplyCommand { get; }
    public RectTransformAnchorPreset Preset { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public RectTransformAnchorPresetViewModel(RectTransformAnchorPreset preset, Action<RectTransformAnchorPreset> applyAction)
    {
        Preset = preset;
        ApplyCommand = new RelayCommand(() => applyAction(preset));
    }
}
