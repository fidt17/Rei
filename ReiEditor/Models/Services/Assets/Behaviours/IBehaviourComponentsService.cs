using System.Collections.Generic;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public interface IBehaviourComponentsService
{
    IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; }

    BehaviourAssetInfo? GetBehaviourById(int id);
    Task<int> ImportBehaviours();

    bool AddComponent(GameEntity e, BehaviourComponent component);
    bool DeleteComponent(GameEntity e, BehaviourComponent component);
}