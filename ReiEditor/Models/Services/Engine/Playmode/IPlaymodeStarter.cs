using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IPlaymodeStarter
{
	ICondition CanStartPlaymode { get; }

	void StartPlaymode();
}