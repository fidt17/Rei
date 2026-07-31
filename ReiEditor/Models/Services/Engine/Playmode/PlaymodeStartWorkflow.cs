using System;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Commands;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeStartWorkflow : IPlaymodeStartWorkflow, IDisposable
{
    private readonly ILogger<PlaymodeStartWorkflow> _logger;
    private readonly IBuildStarter _buildStarter;
    private readonly IEngineRunner _engineRunner;
    private readonly IPlaymodeStarter _playmodeStarter;
    private readonly SaveProjectCommand _saveProjectCommand;

    private int _commandInProgress;

    public PlaymodeStartWorkflow(
        ILogger<PlaymodeStartWorkflow> logger,
        IBuildStarter buildStarter,
        IEngineRunner engineRunner,
        IPlaymodeStarter playmodeStarter,
        IFactory<SaveProjectCommand> saveProjectCommand)
    {
        _logger = logger;
        _buildStarter = buildStarter;
        _engineRunner = engineRunner;
        _playmodeStarter = playmodeStarter;
        _saveProjectCommand = saveProjectCommand.CreateInstance();
    }

    public void Dispose()
    {
        _saveProjectCommand.Dispose();
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_engineRunner.IsPlaymodeActive.Value) return true;
        if (Interlocked.Exchange(ref _commandInProgress, 1) == 1) return false;

        try
        {
            await _engineRunner.StopEngine();
            cancellationToken.ThrowIfCancellationRequested();

            if (!_saveProjectCommand.CanExecute(null))
            {
                _logger.LogError("Cannot start play mode because project cannot be saved.");
                return false;
            }

            await _saveProjectCommand.SaveProject();
            cancellationToken.ThrowIfCancellationRequested();

            var didBuild = await _buildStarter.BuildProject(
                BuildConfigurationEnum.EditorDebug,
                cancellationToken: cancellationToken);
            if (!didBuild)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogError("Cannot start play mode because EditorDebug build failed.");
                return false;
            }

            return await StartEngineAndWaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _commandInProgress, 0);
        }
    }

    private async Task<bool> StartEngineAndWaitAsync(CancellationToken cancellationToken)
    {
        var engineStartRequested = false;
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleStarted() => completionSource.TrySetResult(true);
        void HandleFailed() => completionSource.TrySetResult(false);

        _engineRunner.EngineStartedEvent += HandleStarted;
        _engineRunner.EngineStartFailedEvent += HandleFailed;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_playmodeStarter.TryStart())
            {
                _logger.LogError("Cannot start play mode in current Editor state.");
                return false;
            }

            engineStartRequested = true;
            if (_engineRunner.IsPlaymodeActive.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }

            var didStart = await completionSource.Task.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return didStart;
        }
        catch (OperationCanceledException)
        {
            if (engineStartRequested) ScheduleStopAfterCanceledStart();
            throw;
        }
        finally
        {
            _engineRunner.EngineStartedEvent -= HandleStarted;
            _engineRunner.EngineStartFailedEvent -= HandleFailed;
        }
    }

    private void ScheduleStopAfterCanceledStart()
    {
        var handled = 0;

        void RemoveHandlers()
        {
            _engineRunner.EngineStartedEvent -= HandleStarted;
            _engineRunner.EngineStartFailedEvent -= HandleFailed;
        }

        void HandleStarted()
        {
            if (Interlocked.Exchange(ref handled, 1) == 1) return;
            RemoveHandlers();
            _ = _engineRunner.StopEngine();
        }

        void HandleFailed()
        {
            if (Interlocked.Exchange(ref handled, 1) == 1) return;
            RemoveHandlers();
        }

        _engineRunner.EngineStartedEvent += HandleStarted;
        _engineRunner.EngineStartFailedEvent += HandleFailed;

        if (_engineRunner.IsActive.Value) HandleStarted();
        else if (!_engineRunner.IsEngineStarting.Value) HandleFailed();
    }
}
