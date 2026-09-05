using System.Runtime.InteropServices;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Capture;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using SkiaSharp;

namespace ReiEditor.Tests.Models.Services.Engine.Capture;

/// <summary>Verifies managed frame capture validation, serialization, cancellation, and disposal.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "EngineCapture")]
public sealed class EngineFrameCaptureServiceTests
{
    /// <summary>Accepts or rejects capture requests and invokes managed callbacks explicitly.</summary>
    private sealed class TestCaptureApi : TestEngineApi
    {
        private IntPtr _callback;

        public bool AcceptRequest { get; set; } = true;
        public int RequestCount { get; private set; }
        public TaskCompletionSource RequestReceived { get; private set; } = TestCreateCompletionSource();

        public override bool RequestFrameCapture(IntPtr callback)
        {
            RequestCount++;
            _callback = callback;
            RequestReceived.TrySetResult();
            return AcceptRequest;
        }

        public void Complete(IntPtr pixels, int width, int height)
        {
            var callback = Marshal.GetDelegateForFunctionPointer<IEngineApi.FrameCaptureCallbackDelegate>(_callback);
            callback(pixels, width, height);
        }

        public void ResetRequestSignal() => RequestReceived = TestCreateCompletionSource();

        private static TaskCompletionSource TestCreateCompletionSource()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Managed RGBA pixels encode into a PNG with preserved dimensions and channel values.</summary>
    [Fact]
    public async Task TestCaptureEncodesRgbaPixelsAsPng()
    {
        var (service, api, _) = CreateService();
        using (service)
        {
            var capture = service.CaptureAsync();
            await api.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var pixels = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 };
            var pointer = Marshal.AllocHGlobal(pixels.Length);
            try
            {
                Marshal.Copy(pixels, 0, pointer, pixels.Length);
                api.Complete(pointer, 2, 1);
                var result = await capture.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(2, result.Width);
                Assert.Equal(1, result.Height);
                using var bitmap = SKBitmap.Decode(result.PngData);
                Assert.NotNull(bitmap);
                Assert.Equal(2, bitmap.Width);
                Assert.Equal(1, bitmap.Height);
                Assert.Equal(SKColors.Red, bitmap.GetPixel(0, 0));
                Assert.Equal(SKColors.Lime, bitmap.GetPixel(1, 0));
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }

    /// <summary>Inactive or starting engine rejects capture before native API invocation.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task TestUnavailableEngineRejectsCapture(bool active, bool starting)
    {
        var (service, api, runner) = CreateService(active, starting);
        using (service)
        {
            var exception = await Assert.ThrowsAsync<EngineFrameCaptureException>(() => service.CaptureAsync());

            Assert.Equal("unavailable", exception.Code);
            Assert.Equal(0, api.RequestCount);
        }
    }

    /// <summary>Native request rejection maps to rejected error and allows a subsequent request.</summary>
    [Fact]
    public async Task TestRejectedRequestClearsPendingCapture()
    {
        var (service, api, _) = CreateService();
        using (service)
        {
            api.AcceptRequest = false;
            var exception = await Assert.ThrowsAsync<EngineFrameCaptureException>(() => service.CaptureAsync());
            api.AcceptRequest = true;
            api.ResetRequestSignal();
            var retry = service.CaptureAsync();
            await api.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            api.Complete(IntPtr.Zero, 0, 0);

            Assert.Equal("rejected", exception.Code);
            await Assert.ThrowsAsync<EngineFrameCaptureException>(() => retry);
            Assert.Equal(2, api.RequestCount);
        }
    }

    /// <summary>Null pixels with otherwise valid dimensions map to a failed capture error.</summary>
    [Fact]
    public async Task TestNullFramebufferMapsToFailedError()
    {
        var (service, api, _) = CreateService();
        using (service)
        {
            var capture = service.CaptureAsync();
            await api.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            api.Complete(IntPtr.Zero, 1, 1);

            var exception = await Assert.ThrowsAsync<EngineFrameCaptureException>(() => capture);
            Assert.Equal("failed", exception.Code);
        }
    }

    /// <summary>Nonpositive dimensions with a valid pointer map to failed capture errors before pixel reads.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public async Task TestInvalidFramebufferDimensionsMapToFailedError(int width, int height)
    {
        var (service, api, _) = CreateService();
        using (service)
        {
            var capture = service.CaptureAsync();
            await api.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var pointer = Marshal.AllocHGlobal(4);
            try
            {
                api.Complete(pointer, width, height);
                var exception = await Assert.ThrowsAsync<EngineFrameCaptureException>(() => capture);
                Assert.Equal("failed", exception.Code);
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }

    /// <summary>Oversized dimensions fail before reading supplied pointer memory.</summary>
    [Fact]
    public async Task TestOversizedFramebufferDimensionMapsToTooLargeError()
    {
        var (service, api, _) = CreateService();
        using (service)
        {
            var capture = service.CaptureAsync();
            await api.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Back the advertised frame even if a regression accidentally removes the dimension guard.
            var pixels = new byte[16385 * 4];
            var pointer = Marshal.AllocHGlobal(pixels.Length);
            try
            {
                Marshal.Copy(pixels, 0, pointer, pixels.Length);
                api.Complete(pointer, 16385, 1);
                var exception = await Assert.ThrowsAsync<EngineFrameCaptureException>(() => capture);
                Assert.Equal("too_large", exception.Code);
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }

    /// <summary>Cancellation after acceptance retains serialization until native callback arrives.</summary>
    [Fact]
    public async Task TestCanceledCaptureRetainsSemaphoreUntilNativeCompletion()
    {
        var (service, api, _) = CreateService();
        using (service)
        using (var cancellation = new CancellationTokenSource())
        {
            var firstCapture = service.CaptureAsync(cancellation.Token);
            await api.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstCapture);
            api.ResetRequestSignal();

            var secondCapture = service.CaptureAsync();
            // CaptureAsync reaches the semaphore wait synchronously before returning its task.
            Assert.False(api.RequestReceived.Task.IsCompleted);
            Assert.Equal(1, api.RequestCount);

            api.Complete(IntPtr.Zero, 0, 0);
            await api.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            api.Complete(IntPtr.Zero, 0, 0);
            var exception = await Assert.ThrowsAsync<EngineFrameCaptureException>(() => secondCapture);
            Assert.Equal("failed", exception.Code);
            Assert.Equal(2, api.RequestCount);
        }
    }

    /// <summary>Dispose faults pending capture and rejects later capture requests.</summary>
    [Fact]
    public async Task TestDisposeFaultsPendingAndRejectsNewCapture()
    {
        var (service, api, _) = CreateService();
        var pending = service.CaptureAsync();
        await api.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        service.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.CaptureAsync());
        Assert.Equal(1, api.RequestCount);
    }

    private static (EngineFrameCaptureService Service, TestCaptureApi Api, TestEngineRunner Runner) CreateService(bool active = true, bool starting = false)
    {
        var api = new TestCaptureApi();
        var runner = new TestEngineRunner();
        runner.Active.Value = active;
        runner.Starting.Value = starting;
        return (new EngineFrameCaptureService(api, runner), api, runner);
    }
}
