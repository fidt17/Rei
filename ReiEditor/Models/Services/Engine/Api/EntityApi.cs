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
    public GetEntityDataResponse? GetEntityData(int sceneEntityId)
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
    public bool RenameEntity(int sceneEntityId, string newName)
    {
        if (!_engineApi.IsEngineRunning) return false;
        
        try
        {
            _engineApi.Invoke(typeof(RenameEntityDelegate), "RenameEntity", sceneEntityId, newName);
            return true;
        }
        catch (Exception)
        {
            // ignore
        }

        return false;
    }
    
    private delegate void SetEntityDataDelegate(string json);
    public bool SetEntityData(SetEntityDataRequest request)
    {
        if (!_engineApi.IsEngineRunning) return false;
        
        try
        {
            // ReSharper disable once RedundantArgumentDefaultValue
            _engineApi.Invoke(typeof(SetEntityDataDelegate), "SetEntityData", JsonConvert.SerializeObject(request));
            return true;
        }
        catch (Exception)
        {
            // ignore
        }

        return false;
    }
}