using System;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.Services.Scenes;

public sealed class SceneOpeningService : ISceneOpeningService
{
    private readonly IAssetsService _assets;
    private readonly ISceneManagementService _scenes;
    private readonly IEngineRunner _engine;
    private readonly IBuildStarter _build;
    private readonly IEditorProceduresService _procedures;
    private readonly ILogger<SceneOpeningService> _logger;
    private int _opening;

    public SceneOpeningService(IAssetsService assets, ISceneManagementService scenes, IEngineRunner engine, IBuildStarter build, IEditorProceduresService procedures, ILogger<SceneOpeningService> logger)
    {
        _assets = assets;
        _scenes = scenes;
        _engine = engine;
        _build = build;
        _procedures = procedures;
        _logger = logger;
    }

    public async Task<bool> OpenAsync(string assetId)
    {
        if (Interlocked.CompareExchange(ref _opening, 1, 0) != 0) return false;

        var previous = _scenes.CurrentScene.Value;
        var restart = false;
        var switched = false;
        Procedure? procedure = null;
        try
        {
            if (string.IsNullOrWhiteSpace(assetId)) return false;
            if (IsBusy())
            {
                _logger.LogWarning("Cannot open a scene during Play or another Editor operation");
                return false;
            }
            if (previous?.AssetId == assetId) return true;

            var scene = await _assets.Load<Scene>(assetId);
            if (scene == null)
            {
                _logger.LogError($"Cannot load scene {assetId}");
                return false;
            }

            if (IsBusy()) return false;
            procedure = new Procedure(ProcedureTags.LOAD_SCENE);
            _procedures.TrackProcedure(procedure);
            restart = _engine.IsEditorActive.Value;
            await _engine.StopEngine();
            await _assets.SaveProject();
            switched = true;
            await _scenes.LoadScene(scene);
            await _assets.SaveProject();

            // Repack saved scenes before native startup; game build scene indices stay unchanged.
            if (!await _build.BuildProject(BuildConfigurationEnum.EditorDebug, buildSolution: false))
            {
                _logger.LogError("Cannot open scene: asset build failed");
                await RestoreAsync(previous, switched);
                return false;
            }

            if (restart && !_engine.StartEngine(EngineRunMode.EditorMode)) throw new InvalidOperationException("Cannot restart Editor engine for selected scene");
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
            try
            {
                await RestoreAsync(previous, switched);
            }
            catch (Exception restoreException)
            {
                _logger.LogException(restoreException);
            }
            return false;
        }
        finally
        {
            try
            {
                if (restart && !_engine.IsActive.Value && !_engine.IsEngineStarting.Value) _engine.StartEngine(EngineRunMode.EditorMode);
            }
            catch (Exception exception)
            {
                _logger.LogException(exception);
            }
            finally
            {
                Interlocked.Exchange(ref _opening, 0);
                procedure?.Complete();
            }
        }
    }

    private bool IsBusy() => _engine.IsPlaymodeActive.Value || _engine.IsEngineStarting.Value || _assets.SaveInProcess.Value || _procedures.AnyActiveProcedures();

    private async Task RestoreAsync(Scene? previous, bool switched)
    {
        if (!switched || previous == null) return;
        await _scenes.LoadScene(previous);
        await _assets.SaveProject();
    }
}
