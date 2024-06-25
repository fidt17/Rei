using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.Assets.Scripting;

public interface IBehaviourComponentsService
{
    bool AddComponent(GameEntity e, int behaviourId);
    void AddComponent(GameEntity e, string name);
    bool DeleteComponent(GameEntity e, BehaviourComponent component);

    void RefreshComponents(GameEntity e);
}