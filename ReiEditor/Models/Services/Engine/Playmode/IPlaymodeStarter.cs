using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IPlaymodeStarter
{
	ICondition CanStart { get; }

	void Start();
}