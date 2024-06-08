using System;
using System.Threading.Tasks;
using Avalonia.Threading;
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

    private EngineWindowProviderViewModel? _windowProvider = null;
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
	
    private readonly IPlaymodeService _playmodeService;
    private readonly IPlaymodeWindowController _playmodeWindowController;

#pragma warning disable CS8618
    public PlaymodePanelViewModel() { }
#pragma warning restore CS8618

    public PlaymodePanelViewModel(
        IPlaymodeService playmodeService, 
        IFactory<StartPlaymodeCommand> startPlaymodeCommand, 
        IFactory<StopPlaymodeCommand> stopPlaymodeCommand,
        IPlaymodeWindowController playmodeWindowController)
    {
        _playmodeService = playmodeService;
        _playmodeWindowController = playmodeWindowController;
        StartPlaymodeCommand = startPlaymodeCommand.CreateInstance();
        StopPlaymodeCommand = stopPlaymodeCommand.CreateInstance();

        _playmodeService.IsPlaymodeActive.Subscribe(HandlePlaymodeActiveValueChangedEvent);
        _playmodeWindowController.WindowPointer.Subscribe(HandleWindowPointerChangedEvent);
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _playmodeService.IsPlaymodeActive.Unsubscribe(HandlePlaymodeActiveValueChangedEvent);
        _playmodeWindowController.WindowPointer.Unsubscribe(HandleWindowPointerChangedEvent);
		
        StartPlaymodeCommand.Dispose();
        StopPlaymodeCommand.Dispose();
    }

    private void HandleWindowPointerChangedEvent(IntPtr? ptr)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (ptr == null)
            {
                WindowProvider = null;
            }
            else
            {
                WindowProvider = new EngineWindowProviderViewModel(ptr.Value);
            }
        });
    }

    private void HandlePlaymodeActiveValueChangedEvent(bool isActive)
    {
        PlayModeActive = isActive;
    }
}