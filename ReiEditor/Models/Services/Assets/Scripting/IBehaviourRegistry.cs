using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.Scripting;

public interface IBehaviourRegistry
{
    IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; }

    bool TryGetById(int id, [NotNullWhen(returnValue: true)] out BehaviourAssetInfo? behaviour);
    int? GetIdByName(string name);
    Task RefreshBehaviours();
}