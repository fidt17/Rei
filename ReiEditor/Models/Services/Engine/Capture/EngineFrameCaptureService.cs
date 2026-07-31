using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using SkiaSharp;

namespace ReiEditor.Models.Services.Engine.Capture;

internal sealed class EngineFrameCaptureService : IEngineFrameCaptureService, IDisposable
{
    private sealed record RawFrame(byte[] Pixels, int Width, int Height);

    private const int BYTES_PER_PIXEL = 4;
    private const int MAX_DIMENSION = 16384;
    private const int MAX_FRAME_BYTES = 256 * 1024 * 1024;

    private readonly object _sync = new();
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly IEngineApi _engineApi;
    private readonly IEngineRunner _engineRunner;
    private readonly IEngineApi.FrameCaptureCallbackDelegate _captureCallback;

    private TaskCompletionSource<RawFrame>? _pendingCapture;
    private bool _disposed;

    public EngineFrameCaptureService(IEngineApi engineApi, IEngineRunner engineRunner)
    {
        _engineApi = engineApi;
        _engineRunner = engineRunner;
        _captureCallback = HandleFrameCaptured;
    }

    public void Dispose()
    {
        TaskCompletionSource<RawFrame>? pendingCapture;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            pendingCapture = _pendingCapture;
            _pendingCapture = null;
        }

        pendingCapture?.TrySetException(new ObjectDisposedException(nameof(EngineFrameCaptureService)));
    }

    public async Task<EngineFrameCaptureResult> CaptureAsync(CancellationToken cancellationToken = default)
    {
        await _captureLock.WaitAsync(cancellationToken);
        var releaseCaptureLock = true;
        TaskCompletionSource<RawFrame>? completionSource = null;
        var requestAccepted = false;

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_engineRunner.IsActive.Value || _engineRunner.IsEngineStarting.Value)
            {
                throw new EngineFrameCaptureException("unavailable", "Engine must be running and fully started before frame capture.");
            }

            completionSource = new TaskCompletionSource<RawFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _pendingCapture = completionSource;
            }

            var callbackPointer = Marshal.GetFunctionPointerForDelegate(_captureCallback);
            if (!_engineApi.RequestFrameCapture(callbackPointer))
            {
                ClearPendingCapture(completionSource);
                throw new EngineFrameCaptureException("rejected", "Renderer already has a pending frame capture or is unavailable.");
            }

            requestAccepted = true;

            RawFrame rawFrame;
            try
            {
                rawFrame = await completionSource.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                releaseCaptureLock = false;
                _ = ReleaseAfterNativeCompletionAsync(completionSource.Task);
                throw;
            }

            var pngData = await Task.Run(() => EncodePng(rawFrame), cancellationToken);
            return new EngineFrameCaptureResult(pngData, rawFrame.Width, rawFrame.Height);
        }
        finally
        {
            if (completionSource != null && (!requestAccepted || completionSource.Task.IsCompleted)) ClearPendingCapture(completionSource);
            if (releaseCaptureLock) _captureLock.Release();
        }
    }

    private void HandleFrameCaptured(IntPtr pixels, int width, int height)
    {
        TaskCompletionSource<RawFrame>? completionSource;
        lock (_sync)
        {
            completionSource = _pendingCapture;
            _pendingCapture = null;
        }

        if (completionSource == null) return;
        if (pixels == IntPtr.Zero || width <= 0 || height <= 0)
        {
            completionSource.TrySetException(new EngineFrameCaptureException("failed", "Renderer could not provide a framebuffer."));
            return;
        }

        try
        {
            if (width > MAX_DIMENSION || height > MAX_DIMENSION)
            {
                throw new EngineFrameCaptureException("too_large", $"Framebuffer {width}x{height} exceeds capture dimension limit.");
            }

            var byteCount = checked(width * height * BYTES_PER_PIXEL);
            if (byteCount > MAX_FRAME_BYTES)
            {
                throw new EngineFrameCaptureException("too_large", $"Framebuffer requires {byteCount} bytes; limit is {MAX_FRAME_BYTES}.");
            }

            var data = new byte[byteCount];
            Marshal.Copy(pixels, data, 0, byteCount);
            completionSource.TrySetResult(new RawFrame(data, width, height));
        }
        catch (Exception exception)
        {
            completionSource.TrySetException(exception);
        }
    }

    private void ClearPendingCapture(TaskCompletionSource<RawFrame> completionSource)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_pendingCapture, completionSource)) _pendingCapture = null;
        }
    }

    private async Task ReleaseAfterNativeCompletionAsync(Task<RawFrame> captureTask)
    {
        try
        {
            await captureTask;
        }
        catch
        {
        }
        finally
        {
            _captureLock.Release();
        }
    }

    private static byte[] EncodePng(RawFrame frame)
    {
        var imageInfo = new SKImageInfo(frame.Width, frame.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(imageInfo);
        Marshal.Copy(frame.Pixels, 0, bitmap.GetPixels(), frame.Pixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
