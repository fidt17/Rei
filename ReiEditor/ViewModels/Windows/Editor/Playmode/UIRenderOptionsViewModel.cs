using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Render;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class UIRenderOptionsViewModel : BaseViewModel
{
    #region EngineActive

    private bool _engineActive;
    public bool EngineActive
    {
        get => _engineActive;
        private set => SetField(ref _engineActive, value);
    }

    #endregion

    #region RenderUi

    private bool _renderUi = true;
    public bool RenderUi
    {
        get => _renderUi;
        set
        {
            if (SetField(ref _renderUi, value))
            {
                _renderSettingsService.IsUiRenderingEnabled = value;
                _engineApi.ChangeRenderMode(_renderSettingsService.RenderMode, value);
            }
        }
    }

    #endregion

    private readonly IEngineApi _engineApi;
    private readonly IEngineRunner _engineRunner;
    private readonly IRenderSettingsService _renderSettingsService;

#pragma warning disable CS8618
    public UIRenderOptionsViewModel() { }
#pragma warning restore CS8618

    public UIRenderOptionsViewModel(IEngineApi engineApi, IEngineRunner engineRunner, IRenderSettingsService renderSettingsService)
    {
        _engineApi = engineApi;
        _engineRunner = engineRunner;
        _renderSettingsService = renderSettingsService;
        _renderUi = _renderSettingsService.IsUiRenderingEnabled;

        _engineRunner.IsActive.Subscribe(HandleEngineIsActiveValueChangedEvent);
    }

    public override void Dispose()
    {
        base.Dispose();

        _engineRunner.IsActive.Unsubscribe(HandleEngineIsActiveValueChangedEvent);
    }

    private void HandleEngineIsActiveValueChangedEvent(bool isActive)
    {
        EngineActive = isActive;
        if (isActive)
        {
            _engineApi.ChangeRenderMode(_renderSettingsService.RenderMode, RenderUi);
        }
    }
}
