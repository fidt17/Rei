using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Services.Assets.Scripting;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Resolves explicitly configured behaviour names without loading script metadata.</summary>
internal sealed class TestBehaviourRegistry : IBehaviourRegistry
{
    public Dictionary<string, int> Ids { get; } = new();
    public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours => throw new NotSupportedException();
    public int? GetIdByName(string name) => Ids.TryGetValue(name, out var id) ? id : null;
    public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => throw new NotSupportedException();
    public int AllocateBehaviourId() => throw new NotSupportedException();
    public Task RefreshBehaviours() => throw new NotSupportedException();
}
