using System;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Utils.Common.Procedures;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.StatusBar;

public class StatusBarViewModel : BaseViewModel
{
    public bool ShowStatusBar
    {
        get => _showStatusBar;
        private set => SetField(ref _showStatusBar, value);
    }

    public string ActiveProcedureText
    {
        get => _activeProcedureText;
        private set => SetField(ref _activeProcedureText, value);
    }

    private bool _showStatusBar;
    private string _activeProcedureText = "";
    private readonly IEditorProceduresService? _editorProceduresService;
    private int _refreshPending;
    private volatile bool _disposed;

    public StatusBarViewModel() { }

    public StatusBarViewModel(IEditorProceduresService editorProceduresService)
    {
        _editorProceduresService = editorProceduresService;
        editorProceduresService.ProcedureStartedEvent += HandleProcedureChanged;
        editorProceduresService.ProcedureFinishedEvent += HandleProcedureChanged;
        editorProceduresService.ProcedureProgressChangedEvent += HandleProcedureChanged;
        RequestRefresh();
    }

    public override void Dispose()
    {
        _disposed = true;
        if (_editorProceduresService != null)
        {
            _editorProceduresService.ProcedureStartedEvent -= HandleProcedureChanged;
            _editorProceduresService.ProcedureFinishedEvent -= HandleProcedureChanged;
            _editorProceduresService.ProcedureProgressChangedEvent -= HandleProcedureChanged;
        }
        base.Dispose();
    }

    private void HandleProcedureChanged(IProcedure _) => RequestRefresh();

    private void RequestRefresh()
    {
        if (_disposed) return;
        if (Dispatcher.UIThread.CheckAccess())
        {
            Refresh();
            return;
        }
        if (Interlocked.Exchange(ref _refreshPending, 1) != 0) return;
        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _refreshPending, 0);
            if (!_disposed) Refresh();
        });
    }

    private void Refresh()
    {
        var procedure = _editorProceduresService?.ActiveProcedures.LastOrDefault();
        ShowStatusBar = procedure != null;
        var progress = procedure?.Progress;
        var percent = progress >= 1 ? 100 : Math.Min(99, (int)Math.Round((progress ?? 0) * 100));
        ActiveProcedureText = procedure == null ? "" : procedure.Name + "..." + (progress.HasValue ? $" {percent}%" : "");
    }
}
