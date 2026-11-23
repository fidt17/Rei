using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.RectTransform;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

public sealed class TestEntityApi : IEntityApi
{
    private readonly List<IReadOnlyList<int>> _selections = new();

    public IReadOnlyList<IReadOnlyList<int>> Selections => _selections;
    public Func<GetSceneEntitiesResponse?>? OnGetSceneEntities { get; set; }
    public Func<int, GetEntityDataResponse?>? OnGetEntityData { get; set; }
    public Action<SetEntityDataRequest>? OnSetData { get; set; }

    public void SetEntitySelection(SetEntitySelectionRequest request) => _selections.Add(request.EntityIds.ToArray());

    public GetSceneEntitiesResponse? GetSceneEntities() => (OnGetSceneEntities ?? throw UnexpectedCall())();
    public GetEntityDataResponse? GetEntityData(int sceneEntityId) => (OnGetEntityData ?? throw UnexpectedCall())(sceneEntityId);
    public InstantiateEntityResponse? CreateNewEntity(string name, int parentId = 0, bool recordHistory = false) => throw UnexpectedCall();
    public void DestroyEntities(IReadOnlyList<int> sceneEntityIds, bool recordHistory) => throw new NotSupportedException();
    public IReadOnlyList<int> InstantiateEntities(IReadOnlyList<InstantiateEntityRequest> requests, bool recordHistory) => throw new NotSupportedException();
    public void DestroyEntity(int sceneEntityId, bool recordHistory = false) => throw UnexpectedCall();
    public void Rename(int sceneEntityId, string newName) => throw UnexpectedCall();
    public void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order, bool recordHierarchyChange = false, RectTransformLayoutData? rectLayout = null) => throw UnexpectedCall();
    public void ReorderEntities(IReadOnlyList<int> entityIds, int insertionIndex, bool recordHierarchyChange = false) => throw new NotSupportedException();
    public void ReparentEntities(IReadOnlyList<int> entityIds, int parentEntityId, int insertionIndex, bool recordHierarchyChange = false, IReadOnlyList<RectTransformLayoutData>? rectLayouts = null) => throw new NotSupportedException();
    public void SetData(SetEntityDataRequest request) => (OnSetData ?? throw UnexpectedCall())(request);
    public InstantiateEntityResponse? InstantiateEntity(InstantiateEntityRequest request) => throw UnexpectedCall();
    public void AddBehaviour(int sceneEntityId, int behaviourId) => throw UnexpectedCall();
    public void DeleteBehaviour(int sceneEntityId, int behaviourId) => throw UnexpectedCall();
    public void SelectEntity(int sceneEntityId, bool resetCurrentSelection = true) => throw UnexpectedCall();
    public void ResetEntitySelection() => throw UnexpectedCall();

    private static InvalidOperationException UnexpectedCall([System.Runtime.CompilerServices.CallerMemberName] string? member = null)
        => new($"Unexpected IEntityApi.{member} call. Configure an explicit test fake for this operation.");
}
