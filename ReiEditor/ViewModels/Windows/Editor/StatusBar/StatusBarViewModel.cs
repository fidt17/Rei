using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Utils.Common.Procedures;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.StatusBar;

public class StatusBarViewModel : BaseViewModel
{
	#region ShowStatusBar

	private bool _showStatusBar;
	public bool ShowStatusBar
	{
		get => _showStatusBar;
		private set => SetField(ref _showStatusBar, value);
	}

	#endregion
	
	#region ActiveProcedureText

	private string _activeProcedureText = "";
	public string ActiveProcedureText
	{
		get => _activeProcedureText;
		private set => SetField(ref _activeProcedureText, value);
	}

	#endregion

	private IProcedure? _activeProcedure;
	private readonly Stack<IProcedure> _runningProcedures = new();
	private readonly IEditorProceduresService _editorProceduresService;

#pragma warning disable CS8618
	public StatusBarViewModel() { }
#pragma warning restore CS8618

	public StatusBarViewModel(IEditorProceduresService editorProceduresService)
	{
		_editorProceduresService = editorProceduresService;
		_editorProceduresService.ProcedureStartedEvent += HandleProcedureStartedEvent;
	}

	public override void Dispose()
	{
		_editorProceduresService.ProcedureStartedEvent -= HandleProcedureStartedEvent;
	}

	private void HandleProcedureStartedEvent(IProcedure procedure)
	{
		DisplayProcedure(procedure);
		
		procedure.FinishedEvent += () =>
		{
			_activeProcedure = null;
			
			var nextProcedure = _editorProceduresService.ActiveProcedures.LastOrDefault();
			if (nextProcedure == null)
			{
				ActiveProcedureText = "";
				ShowStatusBar = false;
				return;
			}
			
			DisplayProcedure(nextProcedure);
		};
	}

	private void DisplayProcedure(IProcedure procedure)
	{
		_activeProcedure = procedure;
		ActiveProcedureText = procedure.Name + "...";
		ShowStatusBar = true;
	}
}