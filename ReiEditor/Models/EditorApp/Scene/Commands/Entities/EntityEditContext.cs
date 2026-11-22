using System;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using EditorScene = ReiEditor.Models.Services.Scenes.Scene;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

internal static class EntityEditContext
{
    public static bool CanEdit(IEngineRunner engine, EditorScene? scene, GameEntity entity) =>
        !engine.IsEngineStarting.Value && !engine.IsPlaymodeActive.Value
        && !(engine.IsActive.Value && engine.ActiveMode == EngineRunMode.PlayMode)
        && ReferenceEquals(scene?.GetById(entity.Id), entity);

    public static GameEntity? FindHistoryEntity(EditorScene scene, GameEntity entity, EditorScene? currentScene)
    {
        if (currentScene == null || !ReferenceEquals(scene.GetById(entity.Id), entity)) return null;
        if (ReferenceEquals(scene, currentScene)) return entity;
        if (string.IsNullOrEmpty(scene.AssetId) || scene.AssetId != currentScene.AssetId) return null;
        return currentScene.GetById(entity.Id);
    }
}
