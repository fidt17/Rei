using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

// The test owns the API (and, for integration tests, its isolated native session).
internal sealed class TestEngineNativeAccess(IEngineApi api) : IEngineNativeAccess
{
    public bool TryInvoke(Action<IEngineApi> action) { action(api); return true; }
}
