using System.Runtime.InteropServices;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Logging;

[Trait("Category", "Unit")]
[Trait("Area", "Console")]
public sealed class EngineLoggerTests
{
    private sealed class Api : TestEngineApi
    {
        public List<IntPtr> Registrations { get; } = [];
        public IEngineApi.IntPtrCallbackDelegate Callback => Marshal.GetDelegateForFunctionPointer<IEngineApi.IntPtrCallbackDelegate>(Registrations[^1]);
        public override void AddLogCallback(IntPtr ptr) => Registrations.Add(ptr);
    }

    [Fact]
    public void BurstIsAvailableImmediatelyWithoutAThrottleAndClearLeavesNoPendingLogs()
    {
        var console = new EditorConsoleService();
        var api = new Api();
        var logger = new EngineLogger(console, api);
        logger.SubscribeToClient();
        Parallel.For(0, 2000, i => Emit(api.Callback, $"asset-{i}", LogLevelEnum.Debug));
        Emit(api.Callback, "scene-ready", LogLevelEnum.Info);

        var logs = console.Logs.ToArray();
        Assert.Equal(2001, logs.Length);
        Assert.Equal(2001, console.LogsCount.Value);
        Assert.Equal(2000, logs.Take(2000).Select(x => x.Message).Distinct().Count());
        Assert.Equal("scene-ready", logs[^1].Message);
        Assert.All(logs, log => Assert.Equal(LogScopeEnum.Engine, log.Scope));
        Assert.Equal("NativeScope\nsource details", logs[^1].Details);

        console.ClearConsole();
        Emit(api.Callback, "after-clear", LogLevelEnum.Warning);
        Assert.Equal("after-clear", Assert.Single(console.Logs).Message);
        GC.KeepAlive(logger);
    }

    [Fact]
    public void ResubscriptionKeepsDelegateAliveAndCanAttachToANewNativeSession()
    {
        var console = new EditorConsoleService();
        var api = new Api();
        var logger = new EngineLogger(console, api);
        logger.SubscribeToClient();
        logger.SubscribeToClient();
        Assert.Equal(api.Registrations[0], api.Registrations[1]);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Emit(api.Callback, "first-session", LogLevelEnum.Info);
        api.Registrations.Clear();
        logger.SubscribeToClient();
        Emit(api.Callback, "replacement-session", LogLevelEnum.Info);
        Assert.Equal(new[] { "first-session", "replacement-session" }, console.Logs.Select(x => x.Message));
        GC.KeepAlive(logger);
    }

    private static void Emit(IEngineApi.IntPtrCallbackDelegate callback, string text, LogLevelEnum level)
    {
        var native = new EngineLogMessage { Scope = "NativeScope", Level = level, Message = text, Details = "source details" };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<EngineLogMessage>());
        try
        {
            Marshal.StructureToPtr(native, pointer, false);
            callback(pointer);
        }
        finally
        {
            Marshal.DestroyStructure<EngineLogMessage>(pointer);
            Marshal.FreeHGlobal(pointer);
        }
    }
}
