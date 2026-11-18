using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.RectTransform;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Api;

public class EntityApi : IEntityApi
{
    private readonly Pool<StringBuilder> _responseBufferPool = new(() => new StringBuilder(16384), x => x.Clear());
    private readonly IEngineApi _engineApi;

    public EntityApi(IEngineApi engineApi)
    {
        _engineApi = engineApi;
    }

    private delegate void GetSceneEntitiesDelegate(StringBuilder outputBuffer, int bufferSize);
    public GetSceneEntitiesResponse? GetSceneEntities()
    {
        if (!_engineApi.IsEngineRunning) return null;

        try
        {
            var buffer = _responseBufferPool.Get();

            _engineApi.Invoke(typeof(GetSceneEntitiesDelegate), "GetSceneEntitiesList", buffer, buffer.Capacity);
            var response = JsonConvert.DeserializeObject<GetSceneEntitiesResponse>(buffer.ToString());

            _responseBufferPool.Put(buffer);

            return response;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private delegate void GetEntityDataDelegate(int sceneEntityId, StringBuilder outputBuffer, int bufferSize);
    public GetEntityDataResponse? GetEntityData(int sceneEntityId)
    {
        if (!_engineApi.IsEngineRunning) return null;

        try
        {
            var buffer = _responseBufferPool.Get();

            _engineApi.Invoke(typeof(GetEntityDataDelegate), "GetEntityData", sceneEntityId, buffer, buffer.Capacity);
            var response = JsonConvert.DeserializeObject<GetEntityDataResponse>(buffer.ToString());

            _responseBufferPool.Put(buffer);

            return response;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private delegate void CreateNewEntityDelegate(string name, StringBuilder outputBuffer, int bufferSize, int requestedId);
    public InstantiateEntityResponse? CreateNewEntity(string name, int requestedId = 0)
    {
        if (!_engineApi.IsEngineRunning) return null;

        var buffer = _responseBufferPool.Get();
        _engineApi.Invoke(typeof(CreateNewEntityDelegate), "CreateNewEntity", name, buffer, buffer.Capacity, requestedId);
        var response = JsonConvert.DeserializeObject<InstantiateEntityResponse>(buffer.ToString());
        _responseBufferPool.Put(buffer);
        return response;
    }

    private delegate void DestroyEntityDelegate(int sceneEntityId);
    public void DestroyEntity(int sceneEntityId)
    {
        if (!_engineApi.IsEngineRunning) return;

        try
        {
            _engineApi.Invoke(typeof(DestroyEntityDelegate), "DestroyEntity", sceneEntityId);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private delegate void RenameEntityDelegate(int sceneEntityId, string newName);
    public void Rename(int sceneEntityId, string newName)
    {
        if (!_engineApi.IsEngineRunning) return;

        try
        {
            _engineApi.Invoke(typeof(RenameEntityDelegate), "RenameEntity", sceneEntityId, newName);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private delegate void SetEntityParentDelegate(int sceneEntityId, int parentSceneEntityId, int order, int recordHierarchyChange, [In] float[]? rectLayout);
    public void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order, bool recordHierarchyChange = false, RectTransformLayoutData? rectLayout = null)
    {
        if (!_engineApi.IsEngineRunning) return;

        try
        {
            float[]? values = rectLayout is { } layout
                ? [layout.AnchoredPosition.X, layout.AnchoredPosition.Y, layout.SizeDelta.X, layout.SizeDelta.Y]
                : null;
            _engineApi.Invoke(typeof(SetEntityParentDelegate), "SetEntityParent", sceneEntityId, parentSceneEntityId, order, recordHierarchyChange ? 1 : 0, values);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private delegate void ReorderEntitiesDelegate([In] int[] entityIds, int count, int insertionIndex, int recordHierarchyChange);
    public void ReorderEntities(IReadOnlyList<int> entityIds, int insertionIndex, bool recordHierarchyChange = false)
    {
        if (!_engineApi.IsEngineRunning || entityIds.Count == 0) return;
        var ids = entityIds.ToArray();
        _engineApi.Invoke(typeof(ReorderEntitiesDelegate), "ReorderEntities", ids, ids.Length, insertionIndex, recordHierarchyChange ? 1 : 0);
    }

    private delegate void ReparentEntitiesDelegate([In] int[] entityIds, int count, int parentEntityId, int insertionIndex, int recordHierarchyChange, [In] float[]? rectLayouts);
    public void ReparentEntities(IReadOnlyList<int> entityIds, int parentEntityId, int insertionIndex, bool recordHierarchyChange = false, IReadOnlyList<RectTransformLayoutData>? rectLayouts = null)
    {
        if (!_engineApi.IsEngineRunning || entityIds.Count == 0) return;
        if (rectLayouts != null && rectLayouts.Count != entityIds.Count) throw new ArgumentException("Each entity must have a RectTransform layout.", nameof(rectLayouts));
        var ids = entityIds.ToArray();
        var values = rectLayouts?.SelectMany(layout => new[] { layout.AnchoredPosition.X, layout.AnchoredPosition.Y, layout.SizeDelta.X, layout.SizeDelta.Y }).ToArray();
        _engineApi.Invoke(typeof(ReparentEntitiesDelegate), "ReparentEntities", ids, ids.Length, parentEntityId, insertionIndex, recordHierarchyChange ? 1 : 0, values);
    }

    private delegate void SetEntityDataDelegate(string json);
    public void SetData(SetEntityDataRequest request)
    {
        if (!_engineApi.IsEngineRunning) return;

        try
        {
            // ReSharper disable once RedundantArgumentDefaultValue
            _engineApi.Invoke(typeof(SetEntityDataDelegate), "SetEntityData", JsonConvert.SerializeObject(request));
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private delegate void InstantiateEntityDelegate(string json, StringBuilder outputBuffer, int bufferSize);
    public InstantiateEntityResponse? InstantiateEntity(InstantiateEntityRequest request)
    {
        if (!_engineApi.IsEngineRunning) return null;

        try
        {
            var buffer = _responseBufferPool.Get();
            _engineApi.Invoke(typeof(InstantiateEntityDelegate), "InstantiateEntity", JsonConvert.SerializeObject(request), buffer, buffer.Capacity);
            var response = JsonConvert.DeserializeObject<InstantiateEntityResponse>(buffer.ToString());
            _responseBufferPool.Put(buffer);
            return response;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private delegate void AddBehaviourDelegate(int sceneEntityId, int behaviourId);
    public void AddBehaviour(int sceneEntityId, int behaviourId)
    {
        if (!_engineApi.IsEngineRunning) return;

        try
        {
            _engineApi.Invoke(typeof(AddBehaviourDelegate), "AddBehaviour", sceneEntityId, behaviourId);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private delegate void DeleteBehaviourDelegate(int sceneEntityId, int behaviourId);
    public void DeleteBehaviour(int sceneEntityId, int behaviourId)
    {
        if (!_engineApi.IsEngineRunning) return;

        try
        {
            _engineApi.Invoke(typeof(DeleteBehaviourDelegate), "DeleteBehaviour", sceneEntityId, behaviourId);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private delegate void SelectEntityDelegate(int sceneEntityId, bool resetCurrentSelection);
    public void SelectEntity(int sceneEntityId, bool resetCurrentSelection = true)
    {
        if (!_engineApi.IsEngineRunning) return;

        try
        {
            _engineApi.Invoke(typeof(SelectEntityDelegate), "SelectEntity", sceneEntityId, resetCurrentSelection);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private delegate void SetEntitySelectionDelegate(string json);
    public void SetEntitySelection(SetEntitySelectionRequest request)
    {
        if (!_engineApi.IsEngineRunning) return;

        try
        {
            _engineApi.Invoke(typeof(SetEntitySelectionDelegate), "SetEntitySelection", JsonConvert.SerializeObject(request));
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private delegate void ResetEntitySelectionDelegate();
    public void ResetEntitySelection()
    {
        if (!_engineApi.IsEngineRunning) return;

        try
        {
            _engineApi.Invoke(typeof(ResetEntitySelectionDelegate), "ResetEntitySelection");
        }
        catch (Exception)
        {
            // ignore
        }
    }
}