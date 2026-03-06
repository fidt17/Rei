using System;

namespace ReiEditor.Utils.Common.Procedures;

public class Procedure : IProcedure
{
	public event Action? FinishedEvent;
	
	public string Name { get; }
	
	public bool Finished { get; private set; }

	public Procedure(string name)
	{
		Name = name;
	}

	public void Complete()
	{
		Finished = true;
		FinishedEvent?.Invoke();
	}
}