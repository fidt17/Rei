using System;

namespace ReiEditor.Utils.Common.Procedures;

public interface IProcedure
{
	event Action FinishedEvent;
	
	string Name { get; }
	bool Finished { get; }
}