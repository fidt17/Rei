using System.Collections.Generic;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.RectTransform;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEntityApi
{
    GetSceneEntitiesResponse? GetSceneEntities();
    
    GetEntityDataResponse? GetEntityData(int sceneEntityId);

    InstantiateEntityResponse? CreateNewEntity(string name, int requestedId = 0);
    void DestroyEntity(int sceneEntityId);
    
    void Rename(int sceneEntityId, string newName);
    void ReorderEntities(IReadOnlyList<int> entityIds, int insertionIndex, bool recordHierarchyChange = false);
    void ReparentEntities(IReadOnlyList<int> entityIds, int parentEntityId, int insertionIndex, bool recordHierarchyChange = false, IReadOnlyList<RectTransformLayoutData>? rectLayouts = null);
    void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order, bool recordHierarchyChange = false, RectTransformLayoutData? rectLayout = null);
    void SetData(SetEntityDataRequest request);
    InstantiateEntityResponse? InstantiateEntity(InstantiateEntityRequest request);

    void AddBehaviour(int sceneEntityId, int behaviourId);
    void DeleteBehaviour(int sceneEntityId, int behaviourId);

    void SelectEntity(int sceneEntityId, bool resetCurrentSelection = true);
    void SetEntitySelection(SetEntitySelectionRequest request);
    void ResetEntitySelection();
}
