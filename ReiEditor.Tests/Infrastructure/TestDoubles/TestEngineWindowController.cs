using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

internal sealed class TestEngineWindowController : IEngineWindowController
{
    public Observable<(int Width, int Height)?> Size { get; } = new(null);
    public ReiEditor.Utils.Common.IObservable<(int Width, int Height)?> ViewportSize => Size;
    public ReiEditor.Utils.Common.IObservable<IntPtr?> WindowPointer => throw new NotSupportedException();
    public void SetupWindow() => throw new NotSupportedException();
    public void DestroyWindow() => throw new NotSupportedException();
    public void SetViewportSize(int width, int height) => throw new NotSupportedException();
}
