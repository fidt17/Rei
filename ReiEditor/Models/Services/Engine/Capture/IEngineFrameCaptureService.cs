using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Engine.Capture;

internal interface IEngineFrameCaptureService
{
    Task<EngineFrameCaptureResult> CaptureAsync(CancellationToken cancellationToken = default);
}

internal sealed record EngineFrameCaptureResult(byte[] PngData, int Width, int Height);

internal sealed class EngineFrameCaptureException : Exception
{
    public string Code { get; }

    public EngineFrameCaptureException(string code, string message) : base(message)
    {
        Code = code;
    }
}
