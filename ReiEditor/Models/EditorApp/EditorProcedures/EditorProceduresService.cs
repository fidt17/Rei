using System;
using System.Collections.Generic;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.EditorApp.EditorProcedures;

public class EditorProceduresService : IEditorProceduresService
{
	public event Action<IProcedure>? ProcedureStartedEvent;
    public event Action<IProcedure>? ProcedureFinishedEvent;
	
	public IEnumerable<IProcedure> ActiveProcedures => _activeProcedures;

	private readonly List<IProcedure> _activeProcedures = new();
	
	public void TrackProcedure(IProcedure procedure)
	{
		if (procedure.Finished) return;
		
		_activeProcedures.Add(procedure);
		procedure.FinishedEvent += () =>
		{
			_activeProcedures.Remove(procedure);
            ProcedureFinishedEvent?.Invoke(procedure);
		};
		ProcedureStartedEvent?.Invoke(procedure);
	}

	public bool AnyActiveProcedures() => _activeProcedures.Count != 0;
}
