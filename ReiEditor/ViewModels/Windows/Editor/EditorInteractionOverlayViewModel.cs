using System.Linq;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Utils.Common.Procedures;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor;

public class EditorInteractionOverlayViewModel : BaseViewModel
{
    private bool _canInteract = true;
    private bool _isVisible;

    public bool CanInteract
    {
        get => _canInteract;
        private set => SetField(ref _canInteract, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (!SetField(ref _isVisible, value)) return;
            CanInteract = !value;
        }
    }

    private readonly IEditorProceduresService _editorProceduresService;

#pragma warning disable CS8618
    public EditorInteractionOverlayViewModel() { }
#pragma warning restore CS8618

    public EditorInteractionOverlayViewModel(IEditorProceduresService editorProceduresService)
    {
        _editorProceduresService = editorProceduresService;
        _editorProceduresService.ProcedureStartedEvent += HandleProcedureChanged;
        _editorProceduresService.ProcedureFinishedEvent += HandleProcedureChanged;
        RefreshVisibility();
    }

    public override void Dispose()
    {
        _editorProceduresService.ProcedureStartedEvent -= HandleProcedureChanged;
        _editorProceduresService.ProcedureFinishedEvent -= HandleProcedureChanged;
    }

    private void HandleProcedureChanged(IProcedure _)
    {
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        IsVisible = _editorProceduresService.ActiveProcedures.Any(ShouldBlockInteraction);
    }

    private static bool ShouldBlockInteraction(IProcedure procedure)
    {
        return procedure.Name is ProcedureTags.SAVE_PROJECT
            or ProcedureTags.IMPORT_ASSETS
            or ProcedureTags.BUILD_PROJECT;
    }
}
