using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Utils.Common.Procedures;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor;

public class EditorInteractionOverlayViewModel : BaseViewModel
{
    private const int HIDE_DELAY_MS = 150;

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
    private CancellationTokenSource? _hideOverlayCancellationTokenSource;

#pragma warning disable CS8618
    public EditorInteractionOverlayViewModel() { }
#pragma warning restore CS8618

    public EditorInteractionOverlayViewModel(IEditorProceduresService editorProceduresService)
    {
        _editorProceduresService = editorProceduresService;
        _editorProceduresService.ProcedureStartedEvent += HandleProcedureChanged;
        _editorProceduresService.ProcedureFinishedEvent += HandleProcedureChanged;
        _ = RefreshVisibility();
    }

    public override void Dispose()
    {
        CancelPendingHide();
        _editorProceduresService.ProcedureStartedEvent -= HandleProcedureChanged;
        _editorProceduresService.ProcedureFinishedEvent -= HandleProcedureChanged;
    }

    private async void HandleProcedureChanged(IProcedure _)
    {
        await RefreshVisibility();
    }

    private async Task RefreshVisibility()
    {
        if (_editorProceduresService.ActiveProcedures.Any(ShouldBlockInteraction))
        {
            CancelPendingHide();
            await Dispatcher.UIThread.InvokeAsync(() => IsVisible = true);
            return;
        }

        await HideOverlayWithDelay();
    }

    private async Task HideOverlayWithDelay()
    {
        CancelPendingHide();
        _hideOverlayCancellationTokenSource = new CancellationTokenSource();

        try
        {
            await Task.Delay(HIDE_DELAY_MS, _hideOverlayCancellationTokenSource.Token);
            await Dispatcher.UIThread.InvokeAsync(() => IsVisible = false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelPendingHide()
    {
        _hideOverlayCancellationTokenSource?.Cancel();
        _hideOverlayCancellationTokenSource?.Dispose();
        _hideOverlayCancellationTokenSource = null;
    }

    private static bool ShouldBlockInteraction(IProcedure procedure)
    {
        return procedure.Name is ProcedureTags.SAVE_PROJECT
            or ProcedureTags.IMPORT_ASSETS
            or ProcedureTags.BUILD_PROJECT;
    }
}
