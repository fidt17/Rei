using System;
using System.Text;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Engine.Api;

public class EntityApi : IEntityApi
{
    private readonly IEngineApi _engineApi;

    public EntityApi(IEngineApi engineApi)
    {
        _engineApi = engineApi;
    }

    private delegate void GetEntityDataDelegate(int sceneEntityId, StringBuilder outputBuffer, int bufferSize);
    public GetEntityDataResponse? GetData(int sceneEntityId)
    {
        if (!_engineApi.IsEngineRunning) return null;
        
        try
        {
            var outputBuffer = new StringBuilder(1024);
            _engineApi.Invoke(typeof(GetEntityDataDelegate), "GetEntityData", sceneEntityId, outputBuffer, outputBuffer.Capacity);

            return JsonConvert.DeserializeObject<GetEntityDataResponse>(outputBuffer.ToString());
        }
        catch (Exception)
        {
            return null;
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
}