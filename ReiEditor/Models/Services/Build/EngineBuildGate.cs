using System;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build;

public class EngineBuildGate : IEngineBuildGate
{
    private readonly IEngineRunner _engineRunner;
    private readonly IClientDllManager _dllManager;
    private readonly ILogger<EngineBuildGate> _logger;

    public EngineBuildGate(IEngineRunner engineRunner, IClientDllManager dllManager, ILogger<EngineBuildGate> logger)
    {
        _engineRunner = engineRunner;
        _dllManager = dllManager;
        _logger = logger;
    }

    public async Task StopEngineAndWaitForDllUnload(CancellationToken cancellationToken)
    {
        await _engineRunner.StopEngine();
        cancellationToken.ThrowIfCancellationRequested();

        var dllWaitUntil = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (_dllManager.DllLoaded.Value && DateTime.UtcNow < dllWaitUntil)
        {
            await Task.Delay(25, cancellationToken);
        }

        if (_dllManager.DllLoaded.Value)
        {
            _logger.LogError("Cannot build: client dll is still loaded after engine stop.");
            throw new Exception("Client dll is still loaded after engine stop.");
        }

        await Task.Delay(250, cancellationToken);
    }
}
