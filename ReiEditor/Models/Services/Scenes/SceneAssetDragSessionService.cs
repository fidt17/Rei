using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Windows.Playmode;

namespace ReiEditor.Models.Services.Scenes;

public sealed class SceneAssetDragSessionService : ISceneAssetDragSessionService
{
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const int CANCEL_DELAY_MS = 100;
    private const int POLL_INTERVAL_MS = 30;
    private const int IDC_ARROW = 32512;

    private readonly ISceneAssetDropService _sceneAssetDropService;
    private readonly IEngineRunner _engineRunner;
    private readonly IEngineApi _engineApi;

    private IReadOnlyList<string> _activeAssetPaths = Array.Empty<string>();
    private CancellationTokenSource? _pollCancellationTokenSource;
    private IntPtr _engineWindowHandle;
    private int _sessionVersion;

    public SceneAssetDragSessionService(
        ISceneAssetDropService sceneAssetDropService,
        IEngineRunner engineRunner,
        IEngineApi engineApi,
        IEngineWindowController engineWindowController)
    {
        _sceneAssetDropService = sceneAssetDropService;
        _engineRunner = engineRunner;
        _engineApi = engineApi;

        engineWindowController.WindowPointer.Subscribe(HandleWindowPointerChanged);
    }

    public bool CanStart(IReadOnlyList<string> assetPaths)
    {
        return _sceneAssetDropService.CanHandleAssetPaths(assetPaths);
    }

    public void Start(IReadOnlyList<string> assetPaths)
    {
        var normalizedAssetPaths = assetPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedAssetPaths.Count == 0) return;
        if (!CanStart(normalizedAssetPaths)) return;

        _sessionVersion++;
        _activeAssetPaths = normalizedAssetPaths;
        StartCursorPolling(_sessionVersion);
    }

    public void HandleDesktopDragCompleted(DragDropEffects result)
    {
        if (_activeAssetPaths.Count == 0) return;
        StopCursorPolling();

        if (result != DragDropEffects.None)
        {
            Cancel();
            return;
        }

        if (IsCursorOverEngineWindow())
        {
            _ = CompleteSceneDropAsync(_activeAssetPaths);
            return;
        }

        _ = CancelAfterDelayAsync(_sessionVersion);
    }

    public void Cancel()
    {
        StopCursorPolling();
        _sessionVersion++;
        _activeAssetPaths = Array.Empty<string>();
    }

    private void HandleWindowPointerChanged(IntPtr? windowPointer)
    {
        if (windowPointer == null)
        {
            _engineWindowHandle = IntPtr.Zero;
            return;
        }

        _engineWindowHandle = _engineApi.GetWindowHandle(windowPointer.Value);
    }

    private async Task CompleteSceneDropAsync(IReadOnlyList<string> assetPaths)
    {
        if (!_engineRunner.IsEditorActive.Value)
        {
            Cancel();
            return;
        }

        if (_engineRunner.IsPlaymodeActive.Value)
        {
            Cancel();
            return;
        }

        Cancel();
        await _sceneAssetDropService.CreateEntitiesFromAssets(assetPaths);
    }

    private async Task CancelAfterDelayAsync(int sessionVersion)
    {
        await Task.Delay(CANCEL_DELAY_MS);
        if (_sessionVersion != sessionVersion) return;

        Cancel();
    }

    private void StartCursorPolling(int sessionVersion)
    {
        StopCursorPolling();

        _pollCancellationTokenSource = new CancellationTokenSource();
        _ = PollCursorAsync(sessionVersion, _pollCancellationTokenSource.Token);
    }

    private void StopCursorPolling()
    {
        _pollCancellationTokenSource?.Cancel();
        _pollCancellationTokenSource?.Dispose();
        _pollCancellationTokenSource = null;
    }

    private async Task PollCursorAsync(int sessionVersion, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _sessionVersion == sessionVersion)
            {
                var isCursorOverEngineWindow = IsCursorOverEngineWindow();
                ForceCursorFeedback(isCursorOverEngineWindow);
                await Task.Delay(POLL_INTERVAL_MS, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool IsCursorOverEngineWindow()
    {
        if (_engineWindowHandle == IntPtr.Zero) return false;
        if (!GetCursorPos(out var cursorPoint)) return false;
        if (!GetWindowRect(_engineWindowHandle, out var windowRect)) return false;

        return cursorPoint.X >= windowRect.Left &&
               cursorPoint.X <= windowRect.Right &&
               cursorPoint.Y >= windowRect.Top &&
               cursorPoint.Y <= windowRect.Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    private void ForceCursorFeedback(bool isCursorOverEngineWindow)
    {
        if (!isCursorOverEngineWindow) return;

        var cursor = LoadCursor(IntPtr.Zero, IDC_ARROW);
        if (cursor == IntPtr.Zero) return;

        SetCursor(cursor);
    }
}
