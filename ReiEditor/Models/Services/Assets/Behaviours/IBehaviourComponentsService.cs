using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public interface IBehaviourComponentsService
{
    bool AddComponent(GameEntity e, int behaviourId);
    bool DeleteComponent(GameEntity e, BehaviourComponent component);

    void RefreshComponents(GameEntity e);
}