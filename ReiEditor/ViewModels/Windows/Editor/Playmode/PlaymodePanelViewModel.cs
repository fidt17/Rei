using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class PlaymodePanelViewModel : BaseViewModel
{
	public StartPlaymodeCommand StartPlaymodeCommand { get; }
	public StopPlaymodeCommand StopPlaymodeCommand { get; }
	
	#region PlayModeActive

	private bool _playModeActive;
	public bool PlayModeActive
	{
		get => _playModeActive;
		private set => SetField(ref _playModeActive, value);
	}

	#endregion
	
	private readonly IPlaymodeService _playmodeService;

#pragma warning disable CS8618
	public PlaymodePanelViewModel() { }
#pragma warning restore CS8618

	public PlaymodePanelViewModel(
		IPlaymodeService playmodeService, 
		IFactory<StartPlaymodeCommand> startPlaymodeCommand, 
		IFactory<StopPlaymodeCommand> stopPlaymodeCommand)
	{
		_playmodeService = playmodeService;
		StartPlaymodeCommand = startPlaymodeCommand.CreateInstance();
		StopPlaymodeCommand = stopPlaymodeCommand.CreateInstance();
		
		_playmodeService.IsPlaymodeActive.Subscribe(HandlePlaymodeActiveValueChangedEvent);
		HandlePlaymodeActiveValueChangedEvent(_playmodeService.IsPlaymodeActive.Value);
	}

	public override void Dispose()
	{
		base.Dispose();
		_playmodeService.IsPlaymodeActive.Unsubscribe(HandlePlaymodeActiveValueChangedEvent);
		
		StartPlaymodeCommand.Dispose();
		StopPlaymodeCommand.Dispose();
	}

	private void HandlePlaymodeActiveValueChangedEvent(bool isActive) => PlayModeActive = isActive;
}