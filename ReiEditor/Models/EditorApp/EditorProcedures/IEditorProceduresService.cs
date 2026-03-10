using System;
using System.Collections.Generic;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.EditorApp.EditorProcedures;

public interface IEditorProceduresService
{
	event Action<IProcedure> ProcedureStartedEvent;
    event Action<IProcedure> ProcedureFinishedEvent;

	IEnumerable<IProcedure> ActiveProcedures { get; }
	
	void TrackProcedure(IProcedure procedure);
	bool AnyActiveProcedures();
}
