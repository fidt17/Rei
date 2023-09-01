using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IPlaymodeService
{
	IObservable<bool> IsPlaymodeActive { get; }

	void StartPlaymode();
	void StopPlaymode();
}