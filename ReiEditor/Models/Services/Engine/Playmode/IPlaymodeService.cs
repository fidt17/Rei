namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IPlaymodeService
{
	Utils.Common.IObservable<bool> IsPlaymodeActive { get; }

	void StartPlaymode();
	void StopPlaymode();
}