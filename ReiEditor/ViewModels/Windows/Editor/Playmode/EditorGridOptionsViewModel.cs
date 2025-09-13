using Newtonsoft.Json;
using ReiEditor.Models.EditorApp.ViewportGrid;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class EditorGridOptionsViewModel : BaseViewModel
{
    #region EngineActive

    private bool _engineActive;
    public bool EngineActive
    {
        get => _engineActive;
        private set => SetField(ref _engineActive, value);
    }

    #endregion
    
    #region XZ

    private bool _xz;
    public bool XZ
    {
        get => _xz;
        set
        {
            if (SetField(ref _xz, value))
            {
                _viewportGridService.EnableXZGrid(value);
                UpdateValues();
            }
        }
    }

    #endregion
    
    #region XY

    private bool _xy;
    public bool XY
    {
        get => _xy;
        set
        {
            if (SetField(ref _xy, value))
            {
                _viewportGridService.EnableXYGrid(value);
                UpdateValues();
            }
        }
    }

    #endregion
    
    #region YZ

    private bool _yz;
    public bool YZ
    {
        get => _yz;
        set
        {
            if (SetField(ref _yz, value))
            {
                _viewportGridService.EnableYZGrid(value);
                UpdateValues();
            }
        }
    }

    #endregion

    #region Opacity

    private float _opacity;
    public float Opacity
    {
        get => _opacity;
        set
        {
            if (SetField(ref _opacity, value))
            {
                _viewportGridService.SetOpacity(value);
                UpdateValues();
            }
        }
    }

    #endregion

    private readonly IViewportGridService _viewportGridService;
    private readonly IEngineRunner _engineRunner;

#pragma warning disable CS8618
    public EditorGridOptionsViewModel() { }
#pragma warning restore CS8618

    public EditorGridOptionsViewModel(IViewportGridService viewportGridService, IEngineRunner engineRunner)
    {
        _viewportGridService = viewportGridService;
        _engineRunner = engineRunner;

        _engineRunner.IsActive.Subscribe(HandleEngineIsActiveValueChangedEvent);
        
        UpdateValues();
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _engineRunner.IsActive.Unsubscribe(HandleEngineIsActiveValueChangedEvent);
    }

    private void HandleEngineIsActiveValueChangedEvent(bool isActive)
    {
        EngineActive = isActive;
    }

    private void UpdateValues()
    {
        var settings = _viewportGridService.GetCurrentSettings();
        XZ = settings.RenderXZ;
        XY = settings.RenderXY;
        YZ = settings.RenderYZ;
        Opacity = settings.Opacity;
    }
}