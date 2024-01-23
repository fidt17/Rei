using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public interface IBehaviourComponentsService
{
    IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; }

    BehaviourAssetInfo? GetBehaviourById(int id);
    Task<int> ImportBehaviours();
}