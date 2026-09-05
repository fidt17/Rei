using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

public sealed class TestEntityApi : IEntityApi
{
    private readonly List<IReadOnlyList<int>> _selections = new();

    public IReadOnlyList<IReadOnlyList<int>> Selections => _selections;
    public Func<GetSceneEntitiesResponse?>? OnGetSceneEntities { get; set; }
    public Func<int, GetEntityDataResponse?>? OnGetEntityData { get; set; }

    public void SetEntitySelection(SetEntitySelectionRequest request) => _selections.Add(request.EntityIds.ToArray());

    public GetSceneEntitiesResponse? GetSceneEntities() => (OnGetSceneEntities ?? throw UnexpectedCall())();
    public GetEntityDataResponse? GetEntityData(int sceneEntityId) => (OnGetEntityData ?? throw UnexpectedCall())(sceneEntityId);
    public InstantiateEntityResponse? CreateNewEntity(string name) => throw UnexpectedCall();
    public void DestroyEntity(int sceneEntityId) => throw UnexpectedCall();
    public void Rename(int sceneEntityId, string newName) => throw UnexpectedCall();
    public void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order) => throw UnexpectedCall();
    public void SetData(SetEntityDataRequest request) => throw UnexpectedCall();
    public InstantiateEntityResponse? InstantiateEntity(InstantiateEntityRequest request) => throw UnexpectedCall();
    public void AddBehaviour(int sceneEntityId, int behaviourId) => throw UnexpectedCall();
    public void DeleteBehaviour(int sceneEntityId, int behaviourId) => throw UnexpectedCall();
    public void SelectEntity(int sceneEntityId, bool resetCurrentSelection = true) => throw UnexpectedCall();
    public void ResetEntitySelection() => throw UnexpectedCall();

    private static InvalidOperationException UnexpectedCall([System.Runtime.CompilerServices.CallerMemberName] string? member = null)
        => new($"Unexpected IEntityApi.{member} call. Configure an explicit test fake for this operation.");
}
