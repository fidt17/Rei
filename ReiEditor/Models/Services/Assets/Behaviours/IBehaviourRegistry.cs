using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public interface IBehaviourRegistry
{
    IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; }

    bool TryGetById(int id, [NotNullWhen(returnValue: true)] out BehaviourAssetInfo? behaviour);
    Task RefreshBehaviours();
}