using System;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;
using ReiEditor.ViewModels.Windows.Editor.Rendering;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class PlaymodePanelViewModel : BaseViewModel
{
    public StartPlaymodeCommand StartPlaymodeCommand { get; }
    public StopPlaymodeCommand StopPlaymodeCommand { get; }

    #region WindowProvider

    private EngineWindowProviderViewModel? _windowProvider;
    public EngineWindowProviderViewModel? WindowProvider
    {
        get => _windowProvider;
        private set => SetField(ref _windowProvider, value);
    }

    #endregion
    
    #region PlayModeActive

    private bool _playModeActive;
    public bool PlayModeActive
    {
        get => _playModeActive;
        private set => SetField(ref _playModeActive, value);
    }

    #endregion
	
    private readonly IEngineRunner _engineRunner;
    private readonly IEngineWindowController _engineWindow;
    private readonly IEngineApi _engineApi;

#pragma warning disable CS8618
    public PlaymodePanelViewModel()
    {
        _windowProvider = null;
    }
#pragma warning restore CS8618

    public PlaymodePanelViewModel(
        IFactory<StartPlaymodeCommand> startPlaymodeCommand, 
        IFactory<StopPlaymodeCommand> stopPlaymodeCommand,
        IEngineWindowController engineWindow,
        IEngineApi engineApi, IEngineRunner engineRunner)
    {
        _windowProvider = null;
        _engineWindow = engineWindow;
        _engineApi = engineApi;
        _engineRunner = engineRunner;
        StartPlaymodeCommand = startPlaymodeCommand.CreateInstance();
        StopPlaymodeCommand = stopPlaymodeCommand.CreateInstance();

        _engineRunner.IsPlaymodeActive.Subscribe(HandlePlaymodeActiveValueChangedEvent);
        _engineWindow.WindowPointer.Subscribe(HandleWindowPointerChangedEvent);
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _engineRunner.IsPlaymodeActive.Unsubscribe(HandlePlaymodeActiveValueChangedEvent);
        _engineWindow.WindowPointer.Unsubscribe(HandleWindowPointerChangedEvent);
		
        StartPlaymodeCommand.Dispose();
        StopPlaymodeCommand.Dispose();
    }

    private void HandleWindowPointerChangedEvent(IntPtr? ptr)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (ptr == null)
            {
                WindowProvider = null;
            }
            else
            {
                WindowProvider = new EngineWindowProviderViewModel(ptr.Value, _engineApi);
            }
        });
    }

    private void HandlePlaymodeActiveValueChangedEvent(bool isActive)
    {
        PlayModeActive = isActive;
    }
}