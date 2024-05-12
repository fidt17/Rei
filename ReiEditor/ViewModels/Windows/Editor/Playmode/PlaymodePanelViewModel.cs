using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class PlaymodePanelViewModel : BaseViewModel
{
    public static PlaymodePanelViewModel Instance;
    
    public StartPlaymodeCommand StartPlaymodeCommand { get; }
    public StopPlaymodeCommand StopPlaymodeCommand { get; }

    public ObservableCollection<PlaymodePanelViewModel> Hack { get; } = new();
    
    #region PlayModeActive

    private bool _playModeActive;
    public bool PlayModeActive
    {
        get => _playModeActive;
        private set => SetField(ref _playModeActive, value);
    }

    #endregion
	
    private readonly IPlaymodeService _playmodeService;
    public readonly IEngineApi _engineApi;
    public bool WindowReady;

#pragma warning disable CS8618
    public PlaymodePanelViewModel() { }
#pragma warning restore CS8618

    public PlaymodePanelViewModel(
        IPlaymodeService playmodeService, 
        IFactory<StartPlaymodeCommand> startPlaymodeCommand, 
        IFactory<StopPlaymodeCommand> stopPlaymodeCommand,
        IEngineApi engineApi)
    {
        _playmodeService = playmodeService;
        _engineApi = engineApi;
        StartPlaymodeCommand = startPlaymodeCommand.CreateInstance();
        StopPlaymodeCommand = stopPlaymodeCommand.CreateInstance();
		
        _playmodeService.IsPlaymodeActive.Subscribe(HandlePlaymodeActiveValueChangedEvent);
        HandlePlaymodeActiveValueChangedEvent(_playmodeService.IsPlaymodeActive.Value);

        Instance = this;
    }

    public override void Dispose()
    {
        base.Dispose();
        _playmodeService.IsPlaymodeActive.Unsubscribe(HandlePlaymodeActiveValueChangedEvent);
		
        StartPlaymodeCommand.Dispose();
        StopPlaymodeCommand.Dispose();
    }

    private void HandlePlaymodeActiveValueChangedEvent(bool isActive)
    {
        PlayModeActive = isActive;

        if (PlayModeActive)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    for (int i = 0; i < 5; i++)
                    {
                        await Task.Delay(1000);
                        var ptr = _engineApi.GetWindowHandle();
                        System.Console.WriteLine($"Window ptr: {ptr}");
                        if (ptr != IntPtr.Zero)
                        {
                            WindowReady = true;
                            Hack.Clear();
                            Hack.Add(this);
                            return;
                        } 
                    }
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(e.ToString());
                }
            });
        }
    }
}