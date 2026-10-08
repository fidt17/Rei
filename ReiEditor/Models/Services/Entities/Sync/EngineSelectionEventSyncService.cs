using System;
using System.Linq;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Selection;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Entities.Sync;

public sealed class EngineSelectionEventSyncService : IEngineSelectionEventSyncService, IDisposable
{
    public bool IsEnabled => _source.IsEventSyncEnabled;

    private readonly IEngineSelectionEventSource _source;
    private readonly ISceneManagementService _scenes;
    private readonly EngineSelectionSyncService _applier;

    public EngineSelectionEventSyncService(IEngineSelectionEventSource source, ISceneManagementService scenes, EngineSelectionSyncService applier)
    {
        _source = source;
        _scenes = scenes;
        _applier = applier;
        _source.SelectionReceived += Receive;
    }

    public void Dispose() => _source.SelectionReceived -= Receive;

    private void Receive(int[] ids)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var scene = _scenes.CurrentScene.Value;
            if (scene != null) _applier.ApplySelection(scene.Entities.ToList(), ids.ToHashSet());
        });
    }
}
