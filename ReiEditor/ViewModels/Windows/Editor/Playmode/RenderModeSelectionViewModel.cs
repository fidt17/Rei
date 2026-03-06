using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Render;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class RenderModeSelectionViewModel : BaseViewModel
{
    #region EngineActive

    private bool _engineActive;
    public bool EngineActive
    {
        get => _engineActive;
        private set => SetField(ref _engineActive, value);
    }

    #endregion
    
    public ObservableCollection<string> Options { get; } = new();
    
    #region SelectedRenderMode

    private string _selectedRenderMode = "";
    public string SelectedRenderMode
    {
        get => _selectedRenderMode;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !_modeNames.ContainsKey(value)) return;
            if (SetField(ref _selectedRenderMode, value))
            {
                _engineApi.ChangeRenderMode(_modeNames[value]);
            }
        }
    }

    #endregion

    private readonly Dictionary<string, RenderMode> _modeNames = new();
    
    private readonly IEngineApi _engineApi;
    private readonly IEngineRunner _engineRunner;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public RenderModeSelectionViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public RenderModeSelectionViewModel(IEngineApi engineApi, IEngineRunner engineRunner)
    {
        _engineApi = engineApi;
        _engineRunner = engineRunner;
        
        ConfigureOptions();
        
        _engineRunner.IsActive.Subscribe(HandleEngineIsActiveValueChangedEvent);
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _engineRunner.IsActive.Unsubscribe(HandleEngineIsActiveValueChangedEvent);
    }

    private void ConfigureOptions()
    {
        _modeNames.Add("Shaded", RenderMode.Shaded);
        _modeNames.Add("Wireframe (Lines)", RenderMode.WireframeLines);
        _modeNames.Add("Wireframe (Points)", RenderMode.WireframePoints);
        _modeNames.Add("Depth", RenderMode.Depth);
        _modeNames.Add("Grayscale", RenderMode.Grayscale);
        _modeNames.Add("Inversion", RenderMode.Inversion);
        _modeNames.Add("BVH", RenderMode.BVH);
        
        foreach (var keyValuePair in _modeNames)
        {
            Options.Add(keyValuePair.Key);
        }
        
        ResetSelection();
    }

    private void HandleEngineIsActiveValueChangedEvent(bool isActive)
    {
        EngineActive = isActive;
        ResetSelection();
    }

    private void ResetSelection()
    {
        SelectedRenderMode = _modeNames.First(x => x.Value == RenderMode.Shaded).Key;
    }
}