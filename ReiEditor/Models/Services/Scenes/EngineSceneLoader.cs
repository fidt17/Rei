using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Scenes;

public sealed class EngineSceneLoader : IEngineSceneLoader
{
    private readonly IEngineNativeAccess _native;
    private readonly ILogger<EngineSceneLoader> _logger;

    public EngineSceneLoader(IEngineNativeAccess native, ILogger<EngineSceneLoader> logger)
    {
        _native = native;
        _logger = logger;
    }

    private string CreateRequest(Scene? scene) => JsonConvert.SerializeObject(new
        {
            SceneId = scene?.AssetId ?? "",
            Scene = scene == null ? null : new
            {
                scene.Name,
                Entities = scene.Entities.Select(entity => new
                {
                    entity.Id, entity.Name,
                    Behaviours = entity.Behaviours.Select(behaviour => new
                    {
                        behaviour.Id,
                        SerializedData = SerializedPropertyJsonConverter.SerializeRuntimeProperties(behaviour.Properties.Values)
                    })
                })
            }
        });

    public async Task<bool> LoadSceneAsync(Scene? scene)
    {
        try
        {
            // Apply the status bar layout before the engine thread stops pumping its window messages.
            // NativeControlHost resizes that window synchronously during layout.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            return await Task.Run(() =>
            {
                var request = CreateRequest(scene);
                var loaded = false;
                return _native.TryInvoke(api => loaded = api.LoadEditorScene(request)) && loaded;
            });
        }
        catch (Exception error)
        {
            _logger.LogException(error);
            return false;
        }
    }
}
