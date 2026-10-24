using System;
using System.Collections.Generic;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.EditorApp.EditorProcedures;

public class EditorProceduresService : IEditorProceduresService
{
    public event Action<IProcedure>? ProcedureStartedEvent;
    public event Action<IProcedure>? ProcedureFinishedEvent;
    public event Action<IProcedure>? ProcedureProgressChangedEvent;

    public IEnumerable<IProcedure> ActiveProcedures { get { lock (_sync) return _activeProcedures.ToArray(); } }

    private readonly object _sync = new();
    private readonly List<IProcedure> _activeProcedures = new();

    public void TrackProcedure(IProcedure procedure)
    {
        lock (_sync)
        {
            if (procedure.Finished) return;
            _activeProcedures.Add(procedure);
            procedure.FinishedEvent += Finished;
            procedure.ProgressChangedEvent += ProgressChanged;
        }
        ProcedureStartedEvent?.Invoke(procedure);

        void ProgressChanged() => ProcedureProgressChangedEvent?.Invoke(procedure);

        void Finished()
        {
            procedure.FinishedEvent -= Finished;
            procedure.ProgressChangedEvent -= ProgressChanged;
            lock (_sync) _activeProcedures.Remove(procedure);
            ProcedureFinishedEvent?.Invoke(procedure);
        }
    }

    public bool AnyActiveProcedures() { lock (_sync) return _activeProcedures.Count != 0; }
}
